using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using NeoExpress.Neo3;

namespace nxp3.Commands
{
    partial class ContractCommand
    {
        [Command(Name = "invoke")]
        class Invoke
        {
            [Argument(0)]
            [Required]
            string InvocationFile { get; } = string.Empty;

            [Argument(1)]
            string Account { get; } = string.Empty;

            [Option]
            bool Test { get; } = false;

            [Option]
            string Input { get; } = string.Empty;

            [Option("--gas|-g", CommandOptionType.SingleValue, Description = "Additional GAS to apply to the contract invocation")]
            decimal AdditionalGas { get; } = 0;

            static void WriteStackItem(IConsole console, Neo.VM.Types.StackItem item, int indent = 1, string prefix = "")
            {
                switch (item)
                {
                    case Neo.VM.Types.Boolean _: 
                        WriteLine(item.GetBoolean() ? "true" : "false");
                        break;
                    case Neo.VM.Types.Integer @int:
                        WriteLine(@int.GetInteger().ToString());
                        break;
                    case Neo.VM.Types.Buffer buffer:
                        WriteLine(Neo.Helper.ToHexString(buffer.GetSpan()));
                        break;
                    case Neo.VM.Types.ByteString byteString:
                        WriteLine(Neo.Helper.ToHexString(byteString.GetSpan()));
                        break;
                    case Neo.VM.Types.Null _:
                        WriteLine("<null>");
                        break;
                    case Neo.VM.Types.Array array:
                        WriteLine($"Array: ({array.Count})");
                        for (int i = 0; i < array.Count; i++)
                        {
                            WriteStackItem(console, array[i], indent + 1);
                        }
                        break;
                    case Neo.VM.Types.Map map:
                        WriteLine($"Map: ({map.Count})");
                        foreach (var m in map)
                        {
                            WriteStackItem(console, m.Key, indent + 1, "key:   ");
                            WriteStackItem(console, m.Value, indent + 1, "value: ");  
                        }
                        break;
                }

                void WriteLine(string value)
                {
                    for (var i = 0; i < indent; i++)
                    {
                        console.Write("  ");
                    }

                    if (!string.IsNullOrEmpty(prefix))
                    {
                        console.Write(prefix);
                    }

                    console.WriteLine(value);
                }
            }

            internal async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
            {
                try
                {
                    if (!File.Exists(InvocationFile))
                    {
                        throw new Exception($"Invocation file {InvocationFile} couldn't be found");
                    }

                    var (chain, _) = Program.LoadExpressChain(Input);
                    var blockchainOperations = new BlockchainOperations();

                    if (Test)
                    {
                        var result = await blockchainOperations.TestInvokeContract(chain, InvocationFile);
                        console.WriteLine($"VM State:     {result.State}");
                        console.WriteLine($"Gas Consumed: {result.GasConsumed}");
                        if (result.Exception != null)
                        {
                            console.WriteLine($"Expception:   {result.Exception}");
                        }
                        if (result.Stack.Length > 0)
                        {
                            var stack = result.Stack;
                            console.WriteLine("Result Stack:");
                            for (int i = 0; i < stack.Length; i++)
                            {
                                WriteStackItem(console, stack[i]);
                            }
                        }
                    }
                    else
                    {
                        var account = blockchainOperations.GetAccount(chain, Account);
                        if (account == null)
                        {
                            throw new Exception($"{Account} account not found.");
                        }
                        var txHash = await blockchainOperations.InvokeContract(chain, InvocationFile, account, AdditionalGas);
                        console.WriteLine($"InvocationTransaction {txHash} submitted");
                    }
                    return 0;
                }
                catch (Exception ex)
                {
                    console.Error.WriteLine(ex.Message);
                    return 1;
                }
            }

        }
    }
}
