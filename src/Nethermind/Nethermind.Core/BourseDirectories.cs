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
    /// Directory holding every Bourse configuration file.
    /// <c>/opt/bourse/data/nethermind</c> on Linux/macOS; the same layout rooted
    /// at <c>C:\</c> on Windows.
    /// </summary>
    public static string ConfigDirectory { get; } = OperatingSystem.IsWindows()
        ? @"C:\opt\bourse\data\nethermind"
        : "/opt/bourse/data/nethermind";

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
