using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualBasic;
using Solnet.Programs;
using Solnet.Programs.Utilities;
using Solnet.Rpc;
using Solnet.Rpc.Core.Http;
using Solnet.Rpc.Models;
using Solnet.Wallet;
using Solnet.Wallet.Utilities;

namespace CandyMachineSniper
{
    class Program
    {
        private static string httpNode = "https://young-shy-rain.solana-mainnet.quiknode.pro/";
        private static string wssNode = "wss://young-shy-rain.solana-mainnet.quiknode.pro/";
        private static string candyMachine = "cndyAnrLdpjq1Ssp1z8xxDsB8dxe7u4HL5Nxi2K5WXZ";

        static async Task Main(string[] args)
        {
            IRpcClient rpc = ClientFactory.GetClient(httpNode);
            IStreamingRpcClient wss = ClientFactory.GetStreamingClient(wssNode);
            
            if (true)
            {
                await AnalyzeCandyMachineTransactions(rpc, candyMachine);
            }
        }

        private static async Task AnalyzeCandyMachineTransactions(IRpcClient rpc, string s)
        {
            var sigs = await GetAddressSignatures(rpc, s);
            var transactions = await GetTransactionDataForSignatures(rpc, sigs);
            var metadatas = Task.WhenAll(transactions.Select(async (o)=>await GetMetadataForTransactionSlotInfo(o)));
        }

        private static dynamic GetMetadataForTransactionSlotInfo(TransactionMetaSlotInfo transaction)
        {
            var isMint = IsTransactionMint(transaction);
            if (isMint)
            {
                var instruction = transaction.Transaction.Message.Instructions.First().Data;
                var bytes = Encoding.ASCII.GetBytes(instruction);
                var instructionData = transaction.Transaction.Message.Instructions[4];
            }
            var isUpload = IsTransactionUpload(transaction);
            if (isUpload)
            {

                var decodedInstructions = InstructionDecoder.DecodeInstructions(transaction);

                var instructionData = (string)decodedInstructions.First().Values.First(o => o.Key == "Data").Value;

                var bytes = new Base58Encoder().DecodeData(instructionData);

                
                var instructionJson = TryDecodeInstructionToUtf8(bytes);
                
                
                Console.WriteLine("Upload transaction found:" + transaction.Slot);
                Console.WriteLine();
                Console.WriteLine("Upload transaction data:");
                foreach ((string name, string uri) element in instructionJson)
                {
                    Console.WriteLine($"Name: {element.name}");
                    Console.WriteLine($"Uri: {element.uri}");
                }
            }

            return null;
        }

        private static List<(string name, string uri)> TryDecodeInstructionToUtf8(
            byte[] bytes)
        {
            try
            {
                var innerSeparator = "3F000000";
                var betweenSeparator = "19000000";
                var firstSeparator = "000000";



                var originalHex = Convert.ToHexString(bytes);


                betweenSeparator = originalHex.Substring(32, 8);

                string hex = "";
                try
                {
                    hex = originalHex.Substring(originalHex.IndexOf(betweenSeparator) + betweenSeparator.Length);
                } catch (Exception e)
                {
                    
                }

                var innerStrings = hex;
                var innerElements = innerStrings.Split(betweenSeparator);
                if (innerElements.Length > 10) innerElements = innerElements.TakeLast(10).ToArray();

                try
                {
                    var firstSeparatorIndex = innerElements.First().IndexOf(firstSeparator);
                    innerSeparator = innerElements.First().Substring(firstSeparatorIndex - 2, 8);

                } catch (Exception e)
                {
                    
                }

                List<(string, string)> innerElementsSeparated = new();
                try
                {
                    innerElementsSeparated = innerElements.Select(o => o.Split(firstSeparator))
                                                              .Select(
                                                                   o => (Encoding.UTF8.GetString(Convert.FromHexString(o[0])),
                                                                       Encoding.UTF8.GetString(Convert.FromHexString(o[1])))
                                                               )
                                                              .ToList();
                } catch (Exception e)
                {
                    
                }

                List<(string, string)> retList = innerElementsSeparated;

                return retList;
            } catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        private static bool IsTransactionUpload(TransactionMetaSlotInfo transaction)
        {
            bool yesItIs = transaction.Meta.LogMessages.Any(o => o.Contains("My position in vec"));
            return yesItIs;
        }

        private static bool IsTransactionMint(TransactionMetaSlotInfo transaction)
        {
            bool yesItIs = transaction.Meta.PreTokenBalances.Length == 0 && transaction.Meta.PostTokenBalances.Length != 0;
            return yesItIs;
        }

        private static async Task<List<TransactionMetaSlotInfo>> GetTransactionDataForSignatures(IRpcClient rpc, List<string> sigs)
        {
            var nextIndex = 0;
            var maxBatchSize = 1000;
            List<TransactionMetaSlotInfo> metaSlots = new List<TransactionMetaSlotInfo>(sigs.Count);
            while (sigs.Count > nextIndex)
            {
                var segment = sigs.Skip(nextIndex).Take(maxBatchSize);
                var resultsBatch = await Task.WhenAll(segment.Select(o => rpc.GetTransactionAsync(o)));
                metaSlots.AddRange(resultsBatch.Select(o=>o.Result));
                nextIndex += maxBatchSize;
            }

            return metaSlots;
        }

        private static async Task<List<string>> GetAddressSignatures(IRpcClient rpc, string programAddress, ulong maxNumber = 10000, string untilTx = null)
        {
            List<string> signatures = new List<string>();
            string before = null;
            while (signatures.Count < (int)maxNumber)
            {
                var tx = await rpc.GetSignaturesForAddressAsync(programAddress, limit:Math.Min(1000,maxNumber-(ulong)signatures.Count), until: untilTx, before: before);
                var results = tx.Result;
                before = results.Last().Signature;
                signatures.AddRange(results.Select(o=>o.Signature));
            }

            return signatures;
        }
    }
}
