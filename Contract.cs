using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;
using Neo.SmartContract.Framework.Services.System;
using System.Numerics;

namespace Raffle
{
    public class Contract : SmartContract
    {
        private const int minAward = 1 * 100000000;

        private static readonly byte[] gas_asset_id = { 231, 45, 40, 105, 121, 238, 108, 177, 183, 230, 93, 253, 223, 178, 227, 132, 16, 11, 141, 20, 142, 119, 88, 222, 66, 228, 22, 139, 113, 121, 44, 96 };

        public static object Main(string operation, byte[] txid)
        {
            if (Runtime.Trigger == TriggerType.Verification)
            {
                Runtime.Log("Verify");
                Transaction tx = (Transaction)ExecutionEngine.ScriptContainer;
                TransactionOutput[] outputs = tx.GetOutputs();
                byte[] receiver = ExecutionEngine.ExecutingScriptHash;
                if (outputs.Length == 1)
                {
                    TransactionOutput output = outputs[0];
                    if (output.ScriptHash == receiver)
                    {
                        return Sweep(tx);
                    }
                    else
                    {
                        return Payout(tx, output);
                    }
                }
                else
                {
                    return false;
                }
            }

            if (Runtime.Trigger == TriggerType.Application)
            {
                Runtime.Log("application");
                if (operation == "last")
                {
                    return LastPayoutTime();
                }

                if (operation == "sweep")
                {
                    Transaction tx = Blockchain.GetTransaction(txid);
                    return Sweep(tx);
                }

                if (operation == "drawing")
                {
                    return FindWinner(txid);
                }
            }

            return false;
        }

        private static bool Sweep(Transaction tx)
        {
            Runtime.Log("Sweep");
            Header header = Blockchain.GetHeader(Blockchain.GetHeight());
            uint elapsed = header.Timestamp - LastPayoutTime();
            uint minDelta = 60;

            // Prevent the sweep from being executed early.
            if (elapsed < minDelta)
            {
                return false;
            }

            long awardTotal = 0;
            foreach (TransactionOutput output in tx.GetReferences())
            {
                if (output.AssetId == gas_asset_id)
                {
                    awardTotal += output.Value;
                }
            }
            // TODO: verify awardTotal == account Balance
            if (awardTotal < minAward) return false;

            return true;
        }

        private static bool FindWinner(byte[] txid)
        {
            Runtime.Log("Find Winner");
            Header header = Blockchain.GetHeader(Blockchain.GetHeight());
            Transaction tx = Blockchain.GetTransaction(txid);
            byte[] receiver = ExecutionEngine.ExecutingScriptHash; // Removing this line breaks everything. Go figure.

            long awardTotal = 0;
            foreach (TransactionOutput output in tx.GetReferences())
            {
                // Should check receiver here too, but sometimes it's None?
                if (output.AssetId == gas_asset_id)
                {
                    awardTotal += output.Value;
                }
            }

            long randomNumber = (long)(header.ConsensusData >> 32);
            long winningTicket = (awardTotal * randomNumber) >> 32;
            long bucket = 0;

            foreach (var input in tx.GetInputs())
            {
                var prevTx = Blockchain.GetTransaction(input.PrevHash);
                var thisOutput = prevTx.GetOutputs()[input.PrevIndex];

                // Should check receiver here too, but sometimes it's None?
                if (thisOutput.AssetId == gas_asset_id)
                {
                    var firstInput = prevTx.GetInputs()[0];
                    var prevOutput = Blockchain.GetTransaction(firstInput.PrevHash).GetOutputs()[firstInput.PrevIndex];
                    var ticketValue = thisOutput.Value;
                    var ticketHolder = prevOutput.ScriptHash;

                    bucket += ticketValue;
                    if (bucket >= winningTicket)
                    {
                        Storage.Put(Storage.CurrentContext, "winner", ticketHolder);
                        Storage.Put(Storage.CurrentContext, "payout", Blockchain.GetHeight());
                        Storage.Put(Storage.CurrentContext, "sweep", tx.Hash);
                        Storage.Put(Storage.CurrentContext, "amount", awardTotal);
                        return true;
                    }
                }
            }
            return true;
        }

        private static bool Payout(Transaction tx, TransactionOutput output)
        {
            Runtime.Log("Payout");
            byte[] winner = Storage.Get(Storage.CurrentContext, "winner");
            byte[] sweep = Storage.Get(Storage.CurrentContext, "sweep");
            BigInteger amount = Storage.Get(Storage.CurrentContext, "amount").AsBigInteger();
            TransactionInput[] inputs = tx.GetInputs();
            if (inputs.Length != 1) return false;
            if (inputs[0].PrevHash == sweep && output.ScriptHash == winner && output.Value == amount)
            {
                return true;
            }
            return false;
        }

        private static uint LastPayoutTime()
        {
            BigInteger lastPayout = Storage.Get(Storage.CurrentContext, "payout").AsBigInteger();
            Header header = Blockchain.GetHeader((uint)lastPayout);
            return header.Timestamp;
        }
    }
}
