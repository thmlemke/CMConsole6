using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Solnet.Programs;
using Solnet.Rpc;
using Solnet.Rpc.Models;
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
            var sigs = await GetAddressSignatures(rpc, s, maxNumber: 30000);
            Console.WriteLine("Signatures collected");
            var transactions = await GetTransactionDataForSignatures(rpc, sigs);
            Console.WriteLine("Transaction data collected");
            var metadataUris = transactions.Select(GetMetadataForTransactionSlotInfo).SelectMany(x=>x).ToArray();
            Console.WriteLine($"{metadataUris.Count()} Metadata Uris collected");
            var metadatas = await Task.WhenAll(metadataUris.Select(async i=> await CollectMetadataFromUri(i)));
            
        }

        private static HttpClient http = new();
        private static async Task<string> CollectMetadataFromUri(string uri)
        {
            var metadataString = await http.GetStringAsync(uri);
            if(metadataString.ToLower().Contains("skyline"))Console.WriteLine(metadataString);
            return metadataString;
        }

        private static List<string> GetMetadataForTransactionSlotInfo(TransactionMetaSlotInfo transaction)
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


                
                var uris = TryDecodeInstructionToUtf8(instructionData);
                
                
                foreach (var element in uris)
                {
                    //Console.WriteLine(element);
                }

                return uris;
            }

            return new List<string>();
        }

        private static List<string> TryDecodeInstructionToUtf8(
            string instructionData)
        {
            //Console.WriteLine("Analyzing string ");
            foreach (var c in instructionData)
            {
               // Console.WriteLine($"Is {c} a space? {Base58Encoder.IsSpace(c)}");
            }

            var bytes = new Base58Encoder().DecodeData(instructionData);
            try
            {
                var innerSeparator = "3F000000";
                var betweenSeparator = "19000000";
                var firstSeparator = "000000";




                var originalHex = Convert.ToHexString(bytes);
                var innerString = Encoding.UTF8.GetString(bytes);

                var uris = GetUrisFromString(innerString);

                return uris;
                
                Console.WriteLine(innerString);

                betweenSeparator = originalHex.Substring(32, 8);

                string hex = "";
                try
                {
                    hex = originalHex.Substring(originalHex.IndexOf(betweenSeparator) + betweenSeparator.Length);
                } catch (Exception e)
                {
                    Console.WriteLine(e);
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
                    Console.WriteLine(e);
                }

                List<(string, string)> innerElementsSeparated = new();
                try
                {
                    innerElementsSeparated = innerElements.Select(o => o.Split(firstSeparator))
                                                              .Select(
                                                                   o => (new string(Encoding.UTF8.GetString(Convert.FromHexString(o[0])).SkipLast(1).ToArray()),
                                                                       Encoding.UTF8.GetString(Convert.FromHexString(o[1])))
                                                               )
                                                              .ToList();
                } catch (Exception e)
                {
                    Console.WriteLine(e);
                }

                List<(string, string)> retList = innerElementsSeparated;

                //return retList;
            } catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        private static List<string> GetUrisFromString(string innerString)
        {
            List<string> list = new();
            int parsedIndex = 0;
            if (innerString.ToLower().Contains("arweave"))
            {
                Console.WriteLine("Arweave link detected");
                while (parsedIndex < innerString.Length)
                {
                    var substring = innerString.Substring(parsedIndex);
                    var httpIndex = substring.ToLower().IndexOf("https://");
                    var containswww = substring.ToLower().Contains("https://www.");
                    if (httpIndex == -1) break;
                    var uri = substring.Substring(httpIndex,
                        "https://www.arweave.net/KPcFffyayPoydX-U2Wjslqg152nu6d4JMzp3RuVGGD4".Length - (containswww ? 0:4));
                    parsedIndex = parsedIndex + httpIndex +
                                  "https://www.arweave.net/KPcFffyayPoydX-U2Wjslqg152nu6d4JMzp3RuVGGD4".Length- (containswww ? 0:4);
                    list.Add(uri);
                    
                }
            }
            else if (innerString.ToLower().Contains("ipfs.io"))
            {
                while (parsedIndex < innerString.Length)
                {
                    var substring = innerString.Substring(parsedIndex);
                    var httpIndex = substring.ToLower().IndexOf("https://");
                    if (httpIndex == -1) break;
                    var jsonIndex = substring.ToLower().IndexOf(".json");
                    if (jsonIndex - httpIndex + 5 < 1) break;
                    var uri = substring.Substring(httpIndex,
                        jsonIndex - httpIndex + 5);
                    parsedIndex = parsedIndex + jsonIndex + 5;
                    list.Add(uri);
                }
            }

            return list;

        }

        private static bool IsTransactionUpload(TransactionMetaSlotInfo transaction)
        {
            try
            {
                bool yesItIs = transaction.Meta.LogMessages.Any(o => o.Contains("My position in vec"));
                return yesItIs;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsTransactionMint(TransactionMetaSlotInfo transaction)
        {
            try
            {
                bool yesItIs = transaction.Meta.PreTokenBalances.Length == 0 &&
                               transaction.Meta.PostTokenBalances.Length != 0;
                return yesItIs;
            }
            catch
            {
                return false;
            }
        }

        private static async Task<List<TransactionMetaSlotInfo>> GetTransactionDataForSignatures(IRpcClient rpc, List<string> sigs)
        {
            var nextIndex = 0;
            var maxBatchSize = 1000;
            List<TransactionMetaSlotInfo> metaSlots = new List<TransactionMetaSlotInfo>(sigs.Count);
            var resultsBatch = await Task.WhenAll(sigs.Select(o => rpc.GetTransactionAsync(o)));
            return resultsBatch.Select(o=>o.Result).ToList();
            while (sigs.Count > nextIndex)
            {
                var segment = sigs.Skip(nextIndex).Take(maxBatchSize);
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
