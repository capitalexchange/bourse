// SPDX-FileCopyrightText: 2026 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using FluentAssertions;
using Nethermind.Blockchain;
using Nethermind.Core;
using Nethermind.Core.Extensions;
using Nethermind.Core.Specs;
using Nethermind.Core.Test;
using Nethermind.Core.Test.Builders;
using Nethermind.Crypto;
using Nethermind.Evm.State;
using Nethermind.Evm.TransactionProcessing;
using Nethermind.Int256;
using Nethermind.Logging;
using Nethermind.Specs;
using Nethermind.Specs.Forks;
using NUnit.Framework;

namespace Nethermind.Evm.Test;

/// <summary>
/// Reproduction harness for the reported "internal value forward fails because the intermediate
/// contract's balance is seen as 0" claim (Bourse bridge: <c>transferRemote{value:X}()</c> →
/// <c>wbourse.deposit{value:X}()</c>).
/// </summary>
/// <remarks>
/// The setup deliberately funds <em>only</em> the sender EOA. The bridge and the inner contract
/// start at a zero balance, so the only way the inner forward can succeed is if the EVM credits
/// the incoming <c>msg.value</c> to the bridge's balance before its frame runs (standard EVM
/// semantics). Run under both a real mined tx (<c>Execute</c>) and eth_call / eth_estimateGas
/// semantics (<c>CallAndRestore</c>, which uses <c>ExecutionOptions.CommitAndRestore</c> =
/// Commit | Restore | SkipValidation).
/// </remarks>
public class BourseValueForwardingTests
{
    private TestSpecProvider _specProvider = null!;
    private IEthereumEcdsa _ethereumEcdsa = null!;
    private ITransactionProcessor _transactionProcessor = null!;
    private IWorldState _stateProvider = null!;
    private IDisposable _worldStateCloser = null!;

    private static readonly Address Bridge = new("0x000000000000000000000000000000000000b71d");
    private static readonly Address Inner = new("0x000000000000000000000000000000000000a111");

    [SetUp]
    public void Setup()
    {
        _specProvider = new TestSpecProvider(Cancun.Instance);

        _stateProvider = TestWorldStateFactory.CreateForTest();
        _worldStateCloser = _stateProvider.BeginScope(IWorldState.PreGenesis);

        // Only the sender is funded. Bridge and inner contract have ZERO balance.
        _stateProvider.CreateAccount(TestItem.AddressA, 1.Ether);

        // Inner contract (wbourse.deposit target): payable, just accepts value and stops.
        byte[] innerCode = Prepare.EvmCode.Op(Instruction.STOP).Done;
        _stateProvider.CreateAccount(Inner, UInt256.Zero);
        _stateProvider.InsertCode(Inner, innerCode, _specProvider.GenesisSpec);

        // Bridge: forward the whole incoming msg.value (CALLVALUE) to the inner contract, then
        // RETURN the 32-byte CALL result so the test can assert the inner call succeeded.
        byte[] bridgeCode = Prepare.EvmCode
            .CallWithValue(Inner, 100000)   // pushes 0,0,0,0,CALLVALUE,Inner,gas; CALL -> result on stack
            .PushData(0)                    // memory offset for the result word
            .Op(Instruction.MSTORE)         // mem[0..32] = call result
            .PushData(32)                   // return length
            .PushData(0)                    // return offset
            .Op(Instruction.RETURN)
            .Done;
        _stateProvider.CreateAccount(Bridge, UInt256.Zero);
        _stateProvider.InsertCode(Bridge, bridgeCode, _specProvider.GenesisSpec);

        _stateProvider.Commit(_specProvider.GenesisSpec);
        _stateProvider.CommitTree(0);

        EthereumCodeInfoRepository codeInfoRepository = new(_stateProvider);
        EthereumVirtualMachine virtualMachine = new(new TestBlockhashProvider(_specProvider), _specProvider, LimboLogs.Instance);
        _transactionProcessor = new EthereumTransactionProcessor(BlobBaseFeeCalculator.Instance, _specProvider, _stateProvider, virtualMachine, codeInfoRepository, LimboLogs.Instance);
        _ethereumEcdsa = new EthereumEcdsa(_specProvider.ChainId);
    }

    [TearDown]
    public void TearDown() => _worldStateCloser.Dispose();

    private Transaction BuildForwardingTx(UInt256 value) => Build.A.Transaction
        .WithGasLimit(200000)
        .WithGasPrice(1)
        .WithValue(value)
        .WithTo(Bridge)
        .WithNonce(_stateProvider.GetNonce(TestItem.AddressA))
        .SignedAndResolved(_ethereumEcdsa, TestItem.PrivateKeyA)
        .TestObject;

    private Block BuildBlock() => Build.A.Block
        .WithNumber(1)
        .WithBaseFeePerGas(0)
        .WithGasLimit(10_000_000)
        .TestObject;

    [Test]
    public void Internal_value_forward_succeeds_in_mined_tx()
    {
        UInt256 value = 1.GWei;
        Transaction tx = BuildForwardingTx(value);
        Block block = BuildBlock();

        TestAllTracerWithOutput tracer = new();
        _transactionProcessor.Execute(tx, new BlockExecutionContext(block.Header, _specProvider.GetSpec(block.Header)), tracer);

        tracer.StatusCode.Should().Be(StatusCode.Success, "the outer tx should not revert");
        // Bridge RETURNs the inner CALL result word; last byte 1 == inner call succeeded.
        tracer.ReturnValue![^1].Should().Be(1, "the inner value-forwarding CALL should succeed");
        _stateProvider.GetBalance(Inner).Should().Be(value, "the forwarded value should land in the inner contract");
    }

    [Test]
    public void Internal_value_forward_succeeds_in_eth_call_estimate_semantics()
    {
        UInt256 value = 1.GWei;
        Transaction tx = BuildForwardingTx(value);
        Block block = BuildBlock();

        TestAllTracerWithOutput tracer = new();
        // CallAndRestore == ExecutionOptions.CommitAndRestore (Commit | Restore | SkipValidation),
        // the exact path eth_call / eth_estimateGas run through.
        _transactionProcessor.CallAndRestore(tx, new BlockExecutionContext(block.Header, _specProvider.GetSpec(block.Header)), tracer);

        tracer.StatusCode.Should().Be(StatusCode.Success, "eth_call/estimateGas of the forwarding tx should not revert");
        tracer.ReturnValue![^1].Should().Be(1, "the inner value-forwarding CALL should succeed under eth_call semantics");
    }
}
