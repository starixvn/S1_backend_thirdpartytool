// See https://aka.ms/new-console-template for more information
using MySql.Data.MySqlClient;
using SlotGameGenerator;
using System.Text.Json;

Console.WriteLine("遊戲開關(0:關閉、1:開啟、3:Release階段)");
var stautsStr = Console.ReadLine();

if (!int.TryParse(stautsStr, out var status)) 
{
    Console.WriteLine("invalid status");
    return;
}
var connectionString = "server=13.251.227.70;port=3306;user=nova88;database=Siebog;password=Aa12345678!";
var connection = new MySqlConnection(connectionString);

var fileName = "slot.json";
var jsonString = File.ReadAllText(fileName);
var slotGames = JsonSerializer.Deserialize<SlotGame>(jsonString)!;

var nextId = await connection.QueryNextGameId(slotGames.TpId);
var tempCurrency = await connection.QueryTpCurrency(slotGames.TpId);

var currencies = tempCurrency.ToList();
if (currencies.Contains(51))
{
    currencies.Remove(51);
    currencies.Add(51);
}

using (StreamWriter writer = new("slot.sql", false))
{
    writer.Write("");
}

CreateSlotGame(nextId, slotGames, WriteFile);
CreateGameCasinoExtra(nextId, slotGames, WriteFile);
CreateGameCasinoInfo(nextId, slotGames, WriteFile);
CreateSlotGameStatus(status, currencies, nextId, slotGames.Game.Count, WriteFile);
static void CreateSlotGame(int nextId, SlotGame slotGame, Action<string> write)
{
    var sql = "INSERT INTO Siebog.SlotGame (Id, TpId, CategoryId, TypeId, GameId, AppId, Fun, Device) VALUES";
    var values = "";
    foreach (var game in slotGame.Game)
    {
        values += $"\r\n({nextId}, {slotGame.TpId}, 2, 1, '{game.GameId}', '{game.GameId}', {game.Fun}, {game.Device}),";
        nextId++;
    }
    values = values.TrimEnd(',') + ";";
    write(sql + values);
}

static void CreateGameCasinoExtra(int nextId, SlotGame slotGame, Action<string> write) 
{
    var sql = "\r\nINSERT INTO Siebog.GameCasinoExtra (TpId, GameCasinoId, Reel, PayLine, RTP) VALUES";
    var values = "";
    foreach (var game in slotGame.Game)
    {
        values += $"\r\n({slotGame.TpId}, {nextId}, '{game.Reel}', '{game.PayLine}', '{game.RTP}'),";
        nextId++;
    }
    values = values.TrimEnd(',') + ";";
    write(sql + values);
}

static void CreateGameCasinoInfo(int nextId, SlotGame slotGame, Action<string> write) 
{
    var sql = "\r\nINSERT INTO Siebog.GameCasinoInfo (TpId, GameCasinoId, Name, Image, Lang) VALUES";
    var values = "";
    foreach (var game in slotGame.Game)
    {
        values += $"\r\n({slotGame.TpId}, {nextId}, '{game.Ename}', 'null', 'en'),";
        values += $"\r\n({slotGame.TpId}, {nextId}, '{game.Cname}', 'null', 'cs'),";
        nextId++;
    }
    values = values.TrimEnd(',') + ";";
    write(sql + values);
}

static void CreateSlotGameStatus(int status, IEnumerable<int> currencies, int nextId, int gameCount, Action<string> write) 
{
    var sql = "\r\nINSERT INTO Siebog.SlotGameStatus (SiteId, CurrencyId, SlotGameId, Status, Operator) VALUES";
    var values = "";
    for (int i = 0; i < gameCount; i++)
    {
        foreach (var currency in currencies)
        {
            if (currency == 51)
            {
                values += $"\r\n(2, 51, {nextId}, {status}, 'Admin'),";
                values += $"\r\n(3, 51, {nextId}, {status}, 'Admin'),";
            }
            else 
            {
                values += $"\r\n(1, {currency}, {nextId}, {status}, 'Admin'),";
            }       
        }
        nextId++;
    }
    values = values.TrimEnd(',') + ";";
    write(sql + values);
}

static void WriteFile(string script) 
{
    Console.WriteLine(script);
    using StreamWriter writer = new ("slot.sql", true);
    writer.Write(script);
}

public class Game
{
    public int? Type { get; set; }
    public string GameId { get; set; }
    public string Ename { get; set; }
    public string Cname { get; set; }
    public string Reel { get; set; }
    public string PayLine { get; set; }
    public string RTP { get; set; }
    public int Fun { get; set; }
    public int Device { get; set; }
}

public class SlotGame
{
    public int TpId { get; set; }
    public IList<Game> Game { get; set; }
}

