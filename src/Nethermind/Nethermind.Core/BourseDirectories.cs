// SPDX-FileCopyrightText: 2026 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.IO;

namespace Nethermind.Core;

/// <summary>
/// Fixed filesystem locations for the Bourse fork deployment.
/// </summary>
/// <remarks>
/// The Bourse fork ships no bundled network presets; every configuration file
/// (node config, chainspec/genesis, etc.) lives together in a single directory
/// so the deployment stays self-contained and predictable.
/// </remarks>
public static class BourseDirectories
{
    /// <summary>
    /// Root of the Bourse data tree: <c>/opt/bourse/data</c> on Linux/macOS,
    /// the same layout rooted at <c>C:\</c> on Windows.
    /// </summary>
    private static string DataRoot { get; } = OperatingSystem.IsWindows()
        ? @"C:\opt\bourse\data"
        : "/opt/bourse/data";

    /// <summary>
    /// Directory holding every Bourse configuration file (node config, chainspec/genesis).
    /// <c>{DataRoot}/nethermind</c>.
    /// </summary>
    public static string ConfigDirectory { get; } = Path.Combine(DataRoot, "nethermind");

    /// <summary>
    /// Shared external directory for state not owned by the node itself.
    /// <c>{DataRoot}/external</c>.
    /// </summary>
    public static string ExternalDirectory { get; } = Path.Combine(DataRoot, "external");

    /// <summary>
    /// Keystore directory. Kept in the shared external tree rather than under the
    /// node's own config directory. <c>{DataRoot}/external/keystore</c>.
    /// </summary>
    public static string KeyStoreDirectory { get; } = Path.Combine(ExternalDirectory, "keystore");

    /// <summary>
    /// Resolves a configuration file path against <see cref="ConfigDirectory"/>.
    /// </summary>
    /// <param name="path">A bare file name, relative path, or absolute path.</param>
    /// <returns>
    /// <paramref name="path"/> unchanged if it is null/empty or already rooted;
    /// otherwise it combined with <see cref="ConfigDirectory"/>.
    /// </returns>
    public static string ResolveConfigPath(string path) =>
        string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path)
            ? path
            : Path.Combine(ConfigDirectory, path);
}
