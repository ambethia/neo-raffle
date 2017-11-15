using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;
using Neo.SmartContract.Framework.Services.System;
using System.Numerics;

namespace Raffle
{
    public class Contract : SmartContract
    {
        private const int minAward = 1 * 100000000;

        // private static readonly byte[] gas_asset_id = { 231, 45, 40, 105, 121, 238, 108, 177, 183, 230, 93, 253, 223, 178, 227, 132, 16, 11, 141, 20, 142, 119, 88, 222, 66, 228, 22, 139, 113, 121, 44, 96 };

        public static object Main(string operation, byte[] txid)
        {
            if (Runtime.Trigger == TriggerType.Verification)
            {
                Transaction tx = (Transaction)ExecutionEngine.ScriptContainer;
                TransactionOutput[] outputs = tx.GetOutputs();
                byte[] receiver = ExecutionEngine.ExecutingScriptHash;
                if (outputs.Length == 1)
                {
                    // If we're sending funds to ourself, then this is a sweep tx,
                    // otherwise it must be a payout.
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

                if (operation == "winner")
                {
                    byte[] winner = Storage.Get(Storage.CurrentContext, "winner");
                    return winner;
                }
            }

            return false;
        }

        // The sweep transaction's purpose is to take all of the deposits into the contract and bundle them into one TX,
        // and TXID makes it easier to enumerate all the transactions in the FindWinner function later.
        private static bool Sweep(Transaction tx)
        {
            Runtime.Log("Sweep");
            Header header = Blockchain.GetHeader(Blockchain.GetHeight());
            uint elapsed = header.Timestamp - LastPayoutTime();
            uint minDelta = 60;
            if (elapsed < minDelta)
            {
                Runtime.Log("Wut");
                return false;
            }

            byte[] gas_asset_id = { 231, 45, 40, 105, 121, 238, 108, 177, 183, 230, 93, 253, 223, 178, 227, 132, 16, 11, 141, 20, 142, 119, 88, 222, 66, 228, 22, 139, 113, 121, 44, 96 };
            Runtime.Notify("gas_asset_id:", gas_asset_id);
            TransactionOutput[] outputs = tx.GetReferences();

            // Sum up the amount in this TX
            long awardTotal = 0;
            foreach (TransactionOutput output in outputs)
            {
                if (output.AssetId == gas_asset_id)
                {
                    awardTotal += output.Value;
                }
            }

            // Verify the minimum payout is met.
            if (awardTotal < minAward) return false;

            return true;
        }

        // Given a TXID, loop through them and pick a winner.
        private static bool FindWinner(byte[] txid)
        {
            Runtime.Log("Find Winner");
            Header header = Blockchain.GetHeader(Blockchain.GetHeight());
            Transaction tx = Blockchain.GetTransaction(txid);
            TransactionOutput[] outputs = tx.GetReferences();
            byte[] gas_asset_id = { 231, 45, 40, 105, 121, 238, 108, 177, 183, 230, 93, 253, 223, 178, 227, 132, 16, 11, 141, 20, 142, 119, 88, 222, 66, 228, 22, 139, 113, 121, 44, 96 };

            // Removing this line breaks everything, even through we dont use it anywhere. Go figure.
            byte[] receiver = ExecutionEngine.ExecutingScriptHash;

            // Get the sum of the gas in this transaction.
            long awardTotal = 0;
            foreach (TransactionOutput output in outputs)
            {
                if (output.AssetId == gas_asset_id)
                {
                    awardTotal += output.Value;
                }
            }

            // We use the nonce from the block to get a random number.
            // It's too big to math with, so we only use half the bits
            long randomNumber = (long)(header.ConsensusData >> 32);
            // Math tricks to pick a number between 0 and awardTotal
            long winningTicket = (awardTotal * randomNumber) >> 32;

            long bucket = 0;
            foreach (var input in tx.GetInputs())
            {
                // Some more trickery to find the senders address.
                var prevTx = Blockchain.GetTransaction(input.PrevHash);
                var thisOutput = prevTx.GetOutputs()[input.PrevIndex];

                if (thisOutput.AssetId == gas_asset_id)
                {
                    var firstInput = prevTx.GetInputs()[0];
                    var prevOutput = Blockchain.GetTransaction(firstInput.PrevHash).GetOutputs()[firstInput.PrevIndex];
                    var ticketValue = thisOutput.Value; // The amount sent in this TX
                    var ticketHolder = prevOutput.ScriptHash; // The sender's address

                    // Add the amount sent into the bucket, once we reach the magic number,
                    // we have a winner, this is the ticket/transaction that includes the winning number.
                    bucket += ticketValue;
                    if (bucket >= winningTicket)
                    {
                        // Store these details so we can use them to generate the payout later.
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

        // Verify the payout transaction is only spending the sweep tx from earlier and is being paid to the winner.
        private static bool Payout(Transaction tx, TransactionOutput output)
        {
            Runtime.Log("Payout");
            byte[] winner = Storage.Get(Storage.CurrentContext, "winner");
            byte[] sweep = Storage.Get(Storage.CurrentContext, "sweep");
            BigInteger amount = Storage.Get(Storage.CurrentContext, "amount").AsBigInteger();
            TransactionInput[] inputs = tx.GetInputs();
            // TODO: add a rake
            if (inputs.Length != 1) return false;
            if (inputs[0].PrevHash == sweep && output.ScriptHash == winner && output.Value >= amount) // gte here so we can send invoke fee amounts that sprinkle in.
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
