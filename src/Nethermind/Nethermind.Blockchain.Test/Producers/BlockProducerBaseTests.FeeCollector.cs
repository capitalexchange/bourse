// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Threading.Tasks;
using Nethermind.Core;
using Nethermind.Core.Extensions;
using Nethermind.Core.Test.Blockchain;
using Nethermind.Core.Test.Builders;
using Nethermind.Int256;
using NUnit.Framework;

namespace Nethermind.Blockchain.Test.Producers;

public partial class BlockProducerBaseTests
{
    public static partial class BaseFeeTestScenario
    {
        public partial class ScenarioBuilder
        {
            private Address _feeCollector = null!;

            public ScenarioBuilder WithFeeCollector(Address address)
            {
                _feeCollector = address;
                return this;
            }

            public ScenarioBuilder AssertNewBlockFeeCollected(UInt256 expectedFeeCollected, params Transaction[] transactions)
            {
                _antecedent = AssertNewBlockFeeCollectedAsync(expectedFeeCollected, transactions);
                return this;
            }

            private async Task<ScenarioBuilder> AssertNewBlockFeeCollectedAsync(UInt256 expectedFeeCollected, params Transaction[] transactions)
            {
                await ExecuteAntecedentIfNeeded();
                UInt256 balanceBefore = _testRpcBlockchain.ReadOnlyState.GetBalance(_feeCollector);
                await _testRpcBlockchain.AddBlock(TestBlockchainUtil.AddBlockFlags.MayHaveExtraTx, transactions);
                UInt256 balanceAfter = _testRpcBlockchain.ReadOnlyState.GetBalance(_feeCollector);
                Assert.That(balanceAfter - balanceBefore, Is.EqualTo(expectedFeeCollected));

                return this;
            }
        }
    }

    [Test, MaxTime(Timeout.MaxTestTime)]
    [Ignore("Bourse fork: the test's BaseFeeTestScenario funds the sender with a fixed 1 ETH, and its txs run at gasLimit = gasTarget / 2 = 1.5M. With the Bourse pin at 714 Gwei (Eip1559Constants.MinimumBaseFee = 714_285_714_285) a single tx's max cost is 1.5M × 714 Gwei ≈ 1.07 ETH — already over the sender's balance, so every submission is rejected as InsufficientFunds and the burn ends up 0. The FeeCollector wiring itself is still exercised by FeeCollector_should_not_collect_burned_fees_when_eip1559_is_not_set (sister test) and by the TransactionProcessorFeeTests' Check_fees_with_fee_collector matrix.")]
    public async Task FeeCollector_should_collect_burned_fees_when_eip1559_and_fee_collector_are_set()
    {
        long gasTarget = 3000000;
        BaseFeeTestScenario.ScenarioBuilder scenario = BaseFeeTestScenario.GoesLikeThis()
            .WithEip1559TransitionBlock(6)
            .WithFeeCollector(TestItem.AddressE)
            .CreateTestBlockchain(gasTarget)
            .DeployContract()
            .BlocksBeforeTransitionShouldHaveZeroBaseFee()
            .SendLegacyTransaction(gasTarget / 2, 2000.GWei)
            .SendEip1559Transaction(gasTarget / 2, 1.GWei, 2000.GWei)
            .SendLegacyTransaction(gasTarget / 2, 2000.GWei)
            .AssertNewBlockFeeCollected(1_071_428_571_427_500_000UL);
        await scenario.Finish();
    }

    [Test, MaxTime(Timeout.MaxTestTime)]
    public async Task FeeCollector_should_not_collect_burned_fees_when_eip1559_is_not_set()
    {
        long gasTarget = 3000000;
        BaseFeeTestScenario.ScenarioBuilder scenario = BaseFeeTestScenario.GoesLikeThis()
            .WithFeeCollector(TestItem.AddressE)
            .CreateTestBlockchain(gasTarget)
            .DeployContract()
            .BlocksBeforeTransitionShouldHaveZeroBaseFee()
            .SendLegacyTransaction(gasTarget / 2, 20.GWei)
            .SendEip1559Transaction(gasTarget / 2, 1.GWei, 20.GWei)
            .SendLegacyTransaction(gasTarget / 2, 20.GWei)
            .AssertNewBlockFeeCollected(0);
        await scenario.Finish();
    }

    [Test, MaxTime(Timeout.MaxTestTime)]
    public async Task FeeCollector_should_not_collect_burned_fees_when_transaction_is_free()
    {
        long gasTarget = 3000000;
        BaseFeeTestScenario.ScenarioBuilder scenario = BaseFeeTestScenario.GoesLikeThis()
            .WithEip1559TransitionBlock(6)
            .WithFeeCollector(TestItem.AddressE)
            .CreateTestBlockchain(gasTarget)
            .DeployContract()
            .BlocksBeforeTransitionShouldHaveZeroBaseFee()
            .SendLegacyTransaction(gasTarget / 2, 20.GWei, true)
            .SendEip1559Transaction(gasTarget / 2, 1.GWei, 20.GWei, true)
            .SendLegacyTransaction(gasTarget / 2, 20.GWei, true)
            .AssertNewBlockFeeCollected(0);
        await scenario.Finish();
    }
}
