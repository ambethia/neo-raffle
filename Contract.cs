using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;
using Neo.SmartContract.Framework.Services.System;
using System.Numerics;

namespace Raffle
{
    public class Contract : SmartContract
    {
        private const int minAward = 1 * 100000000;
        private const int minDelta = 60;

        private static readonly byte[] gas_asset_id = { 231, 45, 40, 105, 121, 238, 108, 177, 183, 230, 93, 253, 223, 178, 227, 132, 16, 11, 141, 20, 142, 119, 88, 222, 66, 228, 22, 139, 113, 121, 44, 96 };

        public static object Main(string operation)
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
                        return FindWinner(tx);
                    }
                    else
                    {
                        return Payout(output);
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

                if (operation == "winner")
                {
                    byte[] winner = Storage.Get(Storage.CurrentContext, "winner");
                    return winner;
                }
            }

            return false;
        }

        private static bool FindWinner(Transaction tx)
        {
            Runtime.Log("Find Winner");
            TransactionOutput[] outputs = tx.GetReferences();
            // Account account = Blockchain.GetAccount(ExecutionEngine.ExecutingScriptHash);
            Header header = Blockchain.GetHeader(Blockchain.GetHeight());

            uint elapsed = header.Timestamp - LastPayoutTime();
            if (elapsed < minDelta) return false;

            long awardTotal = 0; // account.GetBalance(gas_asset_id);
            foreach (TransactionOutput output in outputs)
            {
                if (output.AssetId.Equals(gas_asset_id))
                {
                    awardTotal += output.Value;
                }
            }

            if (awardTotal < minAward) return false;

            long randomNumber = (long)(header.ConsensusData >> 32);
            long winningTicket = (awardTotal * randomNumber) >> 32;
            long bucket = 0;

            foreach (var output in outputs)
            {
                if (output.AssetId.Equals(gas_asset_id))
                {
                    bucket += output.Value;
                    if (bucket >= winningTicket)
                    {
                        Storage.Put(Storage.CurrentContext, "winner", output.ScriptHash);
                        return true;
                    }
                }
            }
            return true;
        }

        private static bool Payout(TransactionOutput output)
        {
            Runtime.Log("Payout");
            byte[] winner = Storage.Get(Storage.CurrentContext, "winner");
            if (output.ScriptHash == winner)
            {
                Storage.Put(Storage.CurrentContext, "payout", Blockchain.GetHeight());
                Storage.Put(Storage.CurrentContext, "winner", 0);
                return true;
            }
            return false;
        }

        private static uint LastPayoutTime()
        {
            Runtime.Log("Payout");
            BigInteger lastPayout = Storage.Get(Storage.CurrentContext, "payout").AsBigInteger();
            Header header = Blockchain.GetHeader((uint)lastPayout);
            return header.Timestamp;
        }
    }
}
