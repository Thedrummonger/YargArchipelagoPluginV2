// See https://aka.ms/new-console-template for more information
using Archipelago.MultiClient.Net;
using YargArchipelagoCommon;

Console.WriteLine("Hello, World!");

var Session = ArchipelagoSessionFactory.CreateSession("localhost");
var result = Session.TryConnectAndLogin("YAYARG", "Player1", Archipelago.MultiClient.Net.Enums.ItemsHandlingFlags.AllItems, new Version(0, 6, 2));

if (result is LoginFailure loginFailure)
{
    Console.WriteLine(string.Join("\n",loginFailure.Errors));
    Console.ReadLine();
    return;
}

var data = CommonData.YargSlotData.Parse(Session.DataStorage.GetSlotData());

Console.WriteLine(Newtonsoft.Json.JsonConvert.SerializeObject(data, Newtonsoft.Json.Formatting.Indented));
File.WriteAllText("SlotData.json", Newtonsoft.Json.JsonConvert.SerializeObject(data, Newtonsoft.Json.Formatting.Indented));

Console.ReadLine();