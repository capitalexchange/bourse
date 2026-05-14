// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Collections.Generic;
using Nethermind.Blockchain.Services;
using Nethermind.Consensus.Clique;
using NSubstitute;
using NUnit.Framework;

namespace Nethermind.Clique.Test
{
    public class CliqueHealthHintServiceTests
    {
        [Test]
        public void GetBlockProcessorAndProducerIntervalHint_returns_expected_result(
            [ValueSource(nameof(BlockProcessorIntervalHintTestCases))]
            BlockProcessorIntervalHint test)
        {
            ISnapshotManager snapshotManager = Substitute.For<ISnapshotManager>();
            snapshotManager.GetLastSignersCount().Returns(test.ValidatorsCount);
            IHealthHintService healthHintService = new CliqueHealthHintService(snapshotManager, test.ChainSpec);
            ulong? actualProcessing = healthHintService.MaxSecondsIntervalForProcessingBlocksHint();
            ulong? actualProducing = healthHintService.MaxSecondsIntervalForProducingBlocksHint();
            Assert.That(actualProcessing, Is.EqualTo(test.ExpectedProcessingHint));
            Assert.That(actualProducing, Is.EqualTo(test.ExpectedProducingHint));
        }

        public class BlockProcessorIntervalHint
        {
            public CliqueChainSpecEngineParameters ChainSpec { get; set; }

            public ulong ValidatorsCount { get; set; }

            public ulong? ExpectedProcessingHint { get; set; }

            public ulong? ExpectedProducingHint { get; set; }

            public override string ToString() =>
                $"SealEngineType: {ChainSpec.SealEngineType}, ValidatorsCount: {ValidatorsCount}, ExpectedProcessingHint: {ExpectedProcessingHint}, ExpectedProducingHint: {ExpectedProducingHint}";
        }

        public static IEnumerable<BlockProcessorIntervalHint> BlockProcessorIntervalHintTestCases
        {
            get
            {
                // Bourse fork: Period is hardcoded to 0 (block-on-demand), so both
                // hints are always 0 regardless of configured period or validator count.
                yield return new BlockProcessorIntervalHint()
                {
                    ChainSpec = new CliqueChainSpecEngineParameters { Period = 0 },
                    ExpectedProcessingHint = 0,
                    ExpectedProducingHint = 0
                };
                yield return new BlockProcessorIntervalHint()
                {
                    ValidatorsCount = 10,
                    ChainSpec = new CliqueChainSpecEngineParameters { Period = 0 },
                    ExpectedProcessingHint = 0,
                    ExpectedProducingHint = 0
                };
                yield return new BlockProcessorIntervalHint()
                {
                    ValidatorsCount = 2,
                    ChainSpec = new CliqueChainSpecEngineParameters { Period = 0 },
                    ExpectedProcessingHint = 0,
                    ExpectedProducingHint = 0
                };
            }
        }
    }
}
