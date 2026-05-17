// SPDX-FileCopyrightText: 2026 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.Text;
using Nethermind.Core;
using Nethermind.Core.Exceptions;
using Nethermind.Core.Extensions;
using Nethermind.Int256;

namespace Nethermind.Config
{
    public class BlocksConfig : IBlocksConfig
    {
        public const int MaxBlockSizeKilobytes = 10240;
        public const int MaxCLWrapperKilobytes = 2048;
        public const int SafetyMarginKilobytes = 256;
        // 7936
        public const int DefaultMaxTxKilobytes = MaxBlockSizeKilobytes - MaxCLWrapperKilobytes - SafetyMarginKilobytes;
        private const string _clientExtraData = "Nethermind";
        public static string DefaultExtraData = _clientExtraData;

        public static void SetDefaultExtraDataWithVersion() => DefaultExtraData = GetDefaultVersionExtraData();

        private byte[] _extraDataBytes = Encoding.UTF8.GetBytes(DefaultExtraData);
        private string _extraDataString = DefaultExtraData;

        private static string GetDefaultVersionExtraData()
        {
            ReadOnlySpan<char> version = ProductInfo.Version.AsSpan();
            int index = version.IndexOfAny('+', '-');
            string alpha = "";
            if (index >= 0)
            {
                if (version[index] == '-')
                {
                    alpha = "a";
                }
            }
            else
            {
                index = version.Length;
            }

            // Don't include too much if the version is long (can be in custom builds)
            index = Math.Min(index, 9);
            string defaultExtraData = $"{_clientExtraData} v{version[..index]}{alpha}";
            return defaultExtraData;
        }

        public bool Enabled { get; set; }
        public long? TargetBlockGasLimit { get; set; } = null;

        // Bourse fork: was 1.Wei. The producer-side MinGasPriceTxFilter rejected any tx whose
        // effective priority fee was below this floor — that meant a tx with maxPriorityFeePerGas=0
        // (the value the patched GasPriceOracle suggests on Bourse) was accepted into the pool
        // but never picked up by the block producer. Drop the floor to 0 so the producer's
        // tx-source pipeline accepts zero-priority txs end-to-end. The basefee redirect
        // (f73bfbf3a4) still pays the validator 1 wei × gasUsed per tx, so the producer isn't
        // working for free.
        public UInt256 MinGasPrice { get; set; } = UInt256.Zero;

        public bool RandomizedBlocks { get; set; }

        public ulong SecondsPerSlot { get; set; } = 12;

        public bool PreWarmStateOnBlockProcessing { get; set; } = true;

        public bool CachePrecompilesOnBlockProcessing { get; set; } = true;

        public int PreWarmStateConcurrency { get; set; } = 0;

        public int BlockProductionTimeoutMs { get; set; } = 4_000;
        public double SingleBlockImprovementOfSlot { get; set; } = 0.25;

        public int GenesisTimeoutMs { get; set; } = 40_000;

        public bool ParallelExecution { get; set; } = true;
        public bool ParallelExecutionBatchRead { get; set; } = true;

        public string ExtraData
        {
            get
            {
                return _extraDataString;
            }
            set
            {
                byte[] bytes = Encoding.UTF8.GetBytes(value);
                if (bytes.Length > 32)
                {
                    throw new InvalidConfigurationException($"Extra Data length was more than 32 bytes. You provided: {_extraDataString}",
                        ExitCodes.TooLongExtraData);

                }

                _extraDataString = value;
                _extraDataBytes = bytes;
            }
        }

        public bool BuildBlocksOnMainState { get; set; }

        public byte[] GetExtraDataBytes() => _extraDataBytes;

        public string GasToken { get => GasTokenTicker; set => GasTokenTicker = value; }

        public static string GasTokenTicker { get; set; } = "ETH";

        public long BlockProductionMaxTxKilobytes { get; set; } = DefaultMaxTxKilobytes;

        public int? BlockProductionBlobLimit { get; set; }
    }
}
