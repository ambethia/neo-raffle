using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;
using Neo.SmartContract.Framework.Services.System;
using System.Numerics;

namespace Raffle
{
    public class Contract : SmartContract
    {
        public static readonly byte[] Owner = { 130, 8, 5, 240, 104, 143, 118, 63, 250, 10, 101, 186, 42, 163, 225, 40, 156, 230, 65, 138 };
        public const long MaxRake = 1;

        private static readonly byte[] gas_asset_id = { 231, 45, 40, 105, 121, 238, 108, 177, 183, 230, 93, 253, 223, 178, 227, 132, 16, 11, 141, 20, 142, 119, 88, 222, 66, 228, 22, 139, 113, 121, 44, 96 };
        private const long factor = 100000000;

        public static object Main(string operation, byte[] txid)
        {
            if (Runtime.Trigger == TriggerType.Verification)
            {
                Transaction tx = (Transaction)ExecutionEngine.ScriptContainer;
                TransactionOutput[] outputs = tx.GetOutputs();
                byte[] receiver = ExecutionEngine.ExecutingScriptHash;

                if (outputs.Length == 1 && outputs[0].ScriptHash == receiver)
                {
                    return Runtime.CheckWitness(Owner);
                }
                else
                {
                    return Payout(tx, outputs);
                }

            }
            if (Runtime.Trigger == TriggerType.Application)
            {
                if (operation == "drawing")
                {
                    return Drawing(txid);
                }
            }
            return false;
        }

        // Given a TXID, loop through them and pick a winner.
        private static bool Drawing(byte[] txid)
        {
            if (!Runtime.CheckWitness(Owner)) return false;

            Header header = Blockchain.GetHeader(Blockchain.GetHeight());
            Transaction tx = Blockchain.GetTransaction(txid);
            TransactionOutput[] outputs = tx.GetReferences();

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
            // We're going to sum up the tickets in the bucket to find the winner.
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

            return false;
        }

        // Verify the payout transaction is only spending the sweep tx from earlier and is being paid to the winner.
        // NOTE: Storage.Get appears to still fail here, making in-contract verification of the winner not possible.
        // However, the winner for each round is still persisted in the blockchain for 3rd-party verification.
        // In theory, uncommenting these lines in the future will allow the contract to verify payouts.
        private static bool Payout(Transaction tx, TransactionOutput[] outputs)
        {
            // byte[] winner = Storage.Get(Storage.CurrentContext, "winner");
            // byte[] sweep = Storage.Get(Storage.CurrentContext, "sweep");
            // TransactionOutput payout = outputs[0];
            TransactionOutput rake = outputs[1];
            TransactionInput[] inputs = tx.GetInputs();

            // if (inputs.Length != 1) return false;

            // if (inputs[0].PrevHash != sweep) return false;

            // if (payout.ScriptHash != winner) return false;

            if (rake.ScriptHash != Owner) return false;

            if (rake.Value > MaxRake * factor) return false;

            return Runtime.CheckWitness(Owner);
        }
    }
}
