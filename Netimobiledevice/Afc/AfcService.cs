using Microsoft.Extensions.Logging;
using Netimobiledevice.Afc.Packets;
using Netimobiledevice.Afc.Responses;
using Netimobiledevice.Lockdown;
using Netimobiledevice.Plist;
using Netimobiledevice.Utils;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Netimobiledevice.Afc
{
    /// <summary>
    /// Interact with the publicly available directories and files
    /// </summary>
    /// <param name="lockdown"></param>
    /// <param name="serviceName"></param>
    /// <param name="logger"></param>
    public class AfcService(LockdownServiceProvider lockdown, string serviceName = "", ILogger? logger = null) : LockdownService(lockdown, GetServiceName(lockdown, serviceName), logger: logger)
    {
        private const string LOCKDOWN_SERVICE_NAME = "com.apple.afc";
        private const string RSD_SERVICE_NAME = "com.apple.afc.shim.remote";

        private const int MAXIMUM_READ_SIZE = 1024 * 1024; // 1 MB

        private static string[] DirectoryTraversalFiles { get; } = [".", "..", ""];

        private ulong _packetNumber;

        private static string GetServiceName(LockdownServiceProvider lockdown, string serviceName)
        {
            if (string.IsNullOrEmpty(serviceName)) {
                if (lockdown is LockdownClient) {
                    return LOCKDOWN_SERVICE_NAME;
                }
                else {
                    return RSD_SERVICE_NAME;
                }
            }
            return serviceName;
        }

        private async ValueTask DispatchPacketAsync(IAfcPacket packet, CancellationToken cancellationToken = default)
        {
            var writer = new AfcPacketWriter(Service.Stream, leaveOpen: true);
            await using (writer.ConfigureAwait(false)) {
                await packet.AcceptAsync(writer, cancellationToken).ConfigureAwait(false);
            }
        }

        private async Task<string> ResolvePath(string filename, CancellationToken cancellationToken)
        {
            DictionaryNode info = await GetFileInfo(filename, cancellationToken).ConfigureAwait(false) ?? [];
            if (info.TryGetValue("st_ifmt", out PropertyNode? stIfmt) && stIfmt.AsStringNode().Value == "S_IFLNK") {
                string target = info["LinkTarget"].AsStringNode().Value;
                if (!target.StartsWith('/')) {
                    // Relative path
                    string filePath = Path.GetDirectoryName(filename) ?? string.Empty;
                    filename = Path.Combine(filePath, target);
                }
                else {
                    filename = target;
                }
            }
            return filename;
        }

        /// <summary>
        /// return true if succeess or raise an exception depending on force parameter.
        /// </summary>
        /// <param name="filename">path to directory or a file</param>
        /// <param name="force">True for ignore exception and return False</param>
        /// <returns></returns>
        private async Task<bool> RmSingle(string filename, bool force, CancellationToken cancellationToken)
        {
            await DispatchPacketAsync(new AfcRmRequest(filename), cancellationToken).ConfigureAwait(false);
            var response = await AfcStatusResponse.ParseAsync(Service.Stream, cancellationToken).ConfigureAwait(false);

            if (force) {
                return response.Error == AfcError.Success;
            }

            response.ThrowIfNotSuccess();
            return true;
        }

        public async Task FileClose(ulong handle, CancellationToken cancellationToken)
        {
            await DispatchPacketAsync(new AfcFileCloseRequest(handle), cancellationToken).ConfigureAwait(false);
            var response = await AfcStatusResponse.ParseAsync(Service.Stream, cancellationToken).ConfigureAwait(false);
            response.ThrowIfNotSuccess();
        }

        public async Task<ulong> FileOpen(string filename, CancellationToken cancellationToken, AfcFileOpenMode mode = AfcFileOpenMode.ReadOnly)
        {
            await DispatchPacketAsync(new AfcFileOpenRequest(mode, filename), cancellationToken).ConfigureAwait(false);
            var response = await AfcFileOpenResponse.ParseAsync(Service.Stream, cancellationToken).ConfigureAwait(false);
            return response.Handle;
        }

        public async Task<byte[]> FileRead(ulong handle, int size, CancellationToken cancellationToken = default)
        {
            var totalRead = 0;
            var dest = new byte[size];
            while (totalRead < size) {
                var read = await FileRead(handle, dest[totalRead..], cancellationToken)
                    .ConfigureAwait(false);
                if (read == 0) {
                    break;
                }
                totalRead += read;
            }

            if (totalRead != size) {
                throw new EndOfStreamException();
            }

            return dest;
        }

        public async Task<int> FileRead(ulong handle, Memory<byte> dest, CancellationToken cancellationToken = default)
        {
            AfcFileReadRequest readRequest = new AfcFileReadRequest(
                handle,
                unchecked((ulong) Math.Max(dest.Length, MAXIMUM_READ_SIZE)));
            await DispatchPacketAsync(readRequest, cancellationToken).ConfigureAwait(false);
            var response = await AfcFileReadResponse.ParseAsync(Service.Stream, dest, cancellationToken).ConfigureAwait(false);
            return response.DataRead;
        }

        /// <summary>
        /// Seeks to a given position of a pre-opened file on the device.
        /// </summary>
        /// <param name="handle">File handle of a previously opened.</param>
        /// <param name="offset">Seek offset.</param>
        /// <param name="whence">Seeking direction, one of SEEK_SET, SEEK_CUR, or SEEK_END.</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        /// <exception cref="AfcException"></exception>
        public async Task FileSeek(ulong handle, long offset, ulong whence, CancellationToken cancellationToken = default)
        {
            await DispatchPacketAsync(new AfcSeekInfoRequest(handle, whence, offset), cancellationToken).ConfigureAwait(false);
            var response = await AfcStatusResponse.ParseAsync(Service.Stream, cancellationToken).ConfigureAwait(false);
            response.ThrowIfNotSuccess();
        }

        /// <summary>
        /// Returns current position in a pre-opened file on the device.
        /// </summary>
        /// <param name="handle">File handle of a previously opened.</param>
        /// <param name="cancellationToken"></param>
        /// <returns>Position in bytes of indicator</returns>
        public async Task<ulong> FileTell(ulong handle, CancellationToken cancellationToken = default)
        {
            await DispatchPacketAsync(new AfcTellRequest(handle), cancellationToken).ConfigureAwait(false);
            var response = await AfcFileTellResponse.ParseAsync(Service.Stream, cancellationToken).ConfigureAwait(false);
            return response.Tell;
        }

        public async Task FileWrite(ulong handle, ReadOnlyMemory<byte> data, CancellationToken cancellationToken, int chunkSize = 4096)
        {
            ulong dataSize = unchecked((ulong) data.Length);
            int chunksCount = (data.Length / chunkSize) + ((dataSize % unchecked((ulong) chunkSize) == 0) ? 0 : 1);
            Logger?.LogDebug("Writing {dataSize} bytes in {chunksCount} chunks", dataSize, chunksCount);

            for (int i = 0; i < chunksCount; ++i) {
                cancellationToken.ThrowIfCancellationRequested();
                Logger?.LogDebug("Writing chunk {i}", i);

                var sliceStart = i * chunkSize;
                var sliceEnd = Math.Min((i + 1) * chunkSize, data.Length);
                AfcFileWriteRequest packet = new AfcFileWriteRequest(
                    handle,
                    data[sliceStart..sliceEnd]);

                await DispatchPacketAsync(packet, cancellationToken).ConfigureAwait(false);
                var response = await AfcStatusResponse.ParseAsync(Service.Stream, cancellationToken).ConfigureAwait(false);
                response.ThrowIfNotSuccess();
            }
        }

        public async Task<bool> Exists(string filename, CancellationToken cancellationToken)
        {
            try {
                await GetFileInfo(filename, cancellationToken).ConfigureAwait(false);
                return true;
            }
            catch (AfcException ex) {
                if (ex.AfcError == AfcError.ObjectNotFound) {
                    return false;
                }
                else {
                    throw;
                }
            }
        }

        public async Task<IReadOnlyList<string>> GetDirectoryList(CancellationToken cancellationToken)
        {
            await DispatchPacketAsync(new AfcFileInfoRequest("/"), cancellationToken).ConfigureAwait(false);
            // TODO RESPONSE
            var response = await AfcDelimitedStringResponse.ParseAsync(Service.Stream, AfcOpCode.GetConInfo, cancellationToken).ConfigureAwait(false);
            return response.Strings;
        }

        public async Task<byte[]?> GetFileContents(string filename, CancellationToken cancellationToken)
        {
            filename = await ResolvePath(filename, cancellationToken).ConfigureAwait(false);

            DictionaryNode info = await GetFileInfo(filename, cancellationToken).ConfigureAwait(false) ?? [];
            if (!info.TryGetValue("st_ifmt", out PropertyNode? stIfmtNode)) {
                throw new AfcException(AfcError.ObjectNotFound, "couldn't find st_ifmt in file info");
            }

            if (stIfmtNode.AsStringNode().Value != "S_IFREG") {
                throw new AfcException(AfcError.InvalidArg, $"{filename} isn't a file");
            }

            ulong handle = await FileOpen(filename, cancellationToken).ConfigureAwait(false);
            if (handle == 0) {
                return null;
            }

            int size = checked((int) info["st_size"].AsIntegerNode().Value);
            byte[] details = await FileRead(handle, size, cancellationToken).ConfigureAwait(false);

            await FileClose(handle, cancellationToken).ConfigureAwait(false);
            return details;
        }

        public async Task<DictionaryNode?> GetFileInfo(string filename, CancellationToken cancellationToken)
        {
            IReadOnlyDictionary<string, string> stat;
            try {

                await DispatchPacketAsync(new AfcFileInfoRequest(filename), cancellationToken).ConfigureAwait(false);
                var response = await AfcFileInfoResponse.ParseAsync(Service.Stream, cancellationToken).ConfigureAwait(false);
                stat = response.Info;
            }
            catch (AfcException ex) {
                if (ex.AfcError != AfcError.ReadError) {
                    throw;
                }
                throw new AfcFileNotFoundException(filename, ex);
            }

            if (stat.Count == 0) {
                return null;
            }

            // Convert timestamps from unix epoch ticks (nanoseconds) to DateTime
            long divisor = (long) Math.Pow(10, 6);
            long mTimeMilliseconds = long.Parse(stat["st_mtime"], CultureInfo.InvariantCulture.NumberFormat) / divisor;
            long birthTimeMilliseconds = long.Parse(stat["st_birthtime"], CultureInfo.InvariantCulture.NumberFormat) / divisor;

            DateTime mTime = DateTimeOffset.FromUnixTimeMilliseconds(mTimeMilliseconds).LocalDateTime;
            DateTime birthTime = DateTimeOffset.FromUnixTimeMilliseconds(birthTimeMilliseconds).LocalDateTime;

            DictionaryNode fileInfo = new DictionaryNode {
                    { "st_ifmt", new StringNode(stat["st_ifmt"]) },
                    { "st_size", new IntegerNode(ulong.Parse(stat["st_size"], CultureInfo.InvariantCulture.NumberFormat)) },
                    { "st_blocks", new IntegerNode(ulong.Parse(stat["st_blocks"], CultureInfo.InvariantCulture.NumberFormat)) },
                    { "st_nlink", new IntegerNode(ulong.Parse(stat["st_nlink"], CultureInfo.InvariantCulture.NumberFormat)) },
                    { "st_mtime", new DateNode(mTime) },
                    { "st_birthtime", new DateNode(birthTime) }
                };

            return fileInfo;
        }

        public async Task<bool> IsDir(string filename, CancellationToken cancellationToken)
        {
            DictionaryNode stat = await GetFileInfo(filename, cancellationToken).ConfigureAwait(false) ?? [];
            if (stat.TryGetValue("st_ifmt", out PropertyNode? value)) {
                return value.AsStringNode().Value == "S_IFDIR";
            }
            return false;
        }

        private async Task<IReadOnlyList<string>> ListDirectory(string filename, CancellationToken cancellationToken)
        {
            await DispatchPacketAsync(new AfcReadDirectoryRequest(filename), cancellationToken).ConfigureAwait(false);
            var response = await AfcDelimitedStringResponse.ParseAsync(Service.Stream, AfcOpCode.GetDeviceInfo, cancellationToken).ConfigureAwait(false);
            return response.Strings;
        }

        public async Task Lock(ulong handle, AfcLockModes operation, CancellationToken cancellationToken)
        {
            await DispatchPacketAsync(new AfcLockRequest(handle, (ulong) operation), cancellationToken).ConfigureAwait(false);
            var response = await AfcStatusResponse.ParseAsync(Service.Stream, cancellationToken).ConfigureAwait(false);
            response.ThrowIfNotSuccess();
        }

        /// <summary>
        /// List the files and folders in the given directory
        /// </summary>
        /// <param name="path">Path to list</param>
        /// <param name="depth">Listing depth, -1 to list infinite depth</param>
        /// <returns>List of files found</returns>
        public async IAsyncEnumerable<string> LsDirectory(string path, [EnumeratorCancellation] CancellationToken cancellationToken, int depth = -1)
        {
            await foreach ((string folder, List<string> dirs, List<string> files) in Walk(path, cancellationToken).ConfigureAwait(false)) {
                if (folder == path) {
                    yield return folder;
                    if (depth == 0) {
                        break;
                    }
                }
                if (folder != path && depth != -1 && folder.Count(x => x == Path.DirectorySeparatorChar) >= depth) {
                    continue;
                }

                List<string> results = [.. dirs.ToArray(), .. files.ToArray()];
                foreach (string entry in results) {
                    yield return $"{folder}/{entry}";
                }
            }
        }

        public async Task Pull(string relativeSrc, string dst, CancellationToken cancellationToken, string srcDir = "")
        {
            string src = srcDir;
            if (string.IsNullOrEmpty(src)) {
                src = relativeSrc;
            }
            else {
                src = $"{src}/{relativeSrc}";
            }

            string[] splitSrc = relativeSrc.Split('/');
            string dstPath = splitSrc.Length > 1 ? Path.Combine(dst, splitSrc[^1]) : Path.Combine(dst, relativeSrc);
            if (OperatingSystem.IsWindows()) {
                // Windows filesystems (NTFS) are more restrictive than unix files systems so we gotta sanitise
                dstPath = PathSanitiser.SantiseWindowsPath(dstPath);
            }
            Logger?.LogInformation("{src} --> {dst}", src, dst);

            src = await ResolvePath(src, cancellationToken).ConfigureAwait(false);
            if (!await IsDir(src, cancellationToken).ConfigureAwait(false)) {
                // Normal file
                using (FileStream fs = new FileStream(dstPath, FileMode.Create)) {
                    byte[]? data = await GetFileContents(src, cancellationToken).ConfigureAwait(false);
                    await fs.WriteAsync(data, cancellationToken).ConfigureAwait(false);
                }
            }
            else {
                // Directory
                Directory.CreateDirectory(dstPath);
                foreach (string filename in await ListDirectory(src, cancellationToken).ConfigureAwait(false)) {
                    string dstFilename = Path.Combine(dstPath, filename);
                    string srcFilename = await ResolvePath($"{src}/{filename}", cancellationToken).ConfigureAwait(false);

                    if (await IsDir(srcFilename, cancellationToken).ConfigureAwait(false)) {
                        Directory.CreateDirectory(dstFilename);
                    }
                    await Pull(srcFilename, dstPath, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// Recursive removal of a directory or a file, if any did not succeed then return list of undeleted filenames or raise exception depending on force parameter.
        /// </summary>
        /// <param name="filename">path to directory or a file</param>
        /// <param name="force">True for ignore exception and return list of undeleted paths</param>
        /// <returns>A list of undeleted paths</returns>
        public async Task<List<string>> Rm(string filename, bool force = false, CancellationToken cancellationToken = default)
        {
            if (!await Exists(filename, cancellationToken).ConfigureAwait(false)) {
                if (!await RmSingle(filename, force, cancellationToken).ConfigureAwait(false)) {
                    return [filename];
                }
            }

            // Single file
            if (!await IsDir(filename, cancellationToken).ConfigureAwait(false)) {
                if (await RmSingle(filename, force, cancellationToken).ConfigureAwait(false)) {
                    return [];
                }
                return [filename];
            }

            // Directory Content
            List<string> undeletedItems = [];
            foreach (string entry in await ListDirectory(filename, cancellationToken).ConfigureAwait(false)) {
                string currentFile = $"{filename}/{entry}";
                if (await IsDir(currentFile, cancellationToken).ConfigureAwait(false)) {
                    List<string> retUndeletedItems = await Rm(currentFile, true, cancellationToken).ConfigureAwait(false);
                    undeletedItems.AddRange(retUndeletedItems);
                }
                else {
                    if (!await RmSingle(currentFile, true, cancellationToken).ConfigureAwait(false)) {
                        undeletedItems.Add(currentFile);
                    }
                }

            }

            // Directory Path
            try {
                if (!await RmSingle(filename, force, cancellationToken).ConfigureAwait(false)) {
                    undeletedItems.Add(filename);
                    return undeletedItems;
                }
            }
            catch (AfcException) {
                if (undeletedItems.Count > 0) {
                    undeletedItems.Add(filename);
                }
                else {
                    throw;
                }
            }

            if (undeletedItems.Count > 0) {
                throw new AfcException($"Failed to delete paths: {string.Join(", ", undeletedItems)}");
            }

            return [];
        }

        public async Task SetFileContents(string filename, byte[] data, CancellationToken cancellationToken)
        {
            ulong handle = await FileOpen(filename, cancellationToken, AfcFileOpenMode.WriteOnly).ConfigureAwait(false);
            if (handle == 0) {
                throw new AfcException(AfcError.OpenFailed, "Failed to open file for writing.");
            }
            await FileWrite(handle, data, cancellationToken).ConfigureAwait(false);
            await FileClose(handle, cancellationToken).ConfigureAwait(false);
        }

        private async IAsyncEnumerable<Tuple<string, List<string>, List<string>>> Walk(string directory, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            List<string> directories = [];
            List<string> files = [];

            foreach (string fd in await ListDirectory(directory, cancellationToken).ConfigureAwait(false)) {
                if (DirectoryTraversalFiles.Contains(fd)) {
                    continue;
                }

                DictionaryNode fileInfo = await GetFileInfo($"{directory}/{fd}", cancellationToken).ConfigureAwait(false) ?? [];
                if (fileInfo.TryGetValue("st_ifmt", out PropertyNode? value)) {
                    if (value is StringNode node && node.Value == "S_IFDIR") {
                        directories.Add(fd);
                    }
                    else {
                        files.Add(fd);
                    }
                }
            }
            yield return Tuple.Create(directory, directories, files);

            foreach (string dir in directories) {
                await foreach (Tuple<string, List<string>, List<string>> result in Walk($"{directory}/{dir}", cancellationToken).ConfigureAwait(false)) {
                    yield return result;
                }
            }
        }
    }
}
