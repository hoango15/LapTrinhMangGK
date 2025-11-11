using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Collections.Concurrent;

const int Port = 5001;
const int BoardSize = 15;

var listener = new TcpListener(IPAddress.Any, Port);
listener.Start();
Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine($"Server CỜ CARO đang chạy tại cổng {Port}");
Console.ResetColor();

var waitingQueue = new ConcurrentQueue<PlayerConn>();
var allClients = new ConcurrentDictionary<TcpClient, PlayerConn>();

_ = AcceptLoop();

Console.CancelKeyPress += (_, __) =>
{
    listener.Stop();
    Console.WriteLine("Server dừng!");
};

await Task.Delay(-1);

async Task AcceptLoop()
{
    while (true)
    {
        var client = await listener.AcceptTcpClientAsync();
        _ = HandleClient(client);
    }
}

async Task HandleClient(TcpClient client)
{
    var stream = client.GetStream();
    var reader = new StreamReader(stream, Encoding.UTF8);
    var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

    try
    {
        // Nhận nickname
        var line = await reader.ReadLineAsync();
        if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("NICK:"))
        {
            await writer.WriteLineAsync("INFO:Nickname không hợp lệ");
            client.Close();
            return;
        }

        var nick = line[5..].Trim();
        var player = new PlayerConn(client, reader, writer, nick);
        allClients[client] = player;

        Console.WriteLine($"🧍 {nick} đã kết nối.");
        await writer.WriteLineAsync("INFO:Chờ ghép đối thủ...");

        while (true)
        {
            // Nếu chưa có ai chờ, đưa vào hàng đợi
            if (!waitingQueue.TryDequeue(out var opponent))
            {
                waitingQueue.Enqueue(player);
                break;
            }

            // Ghép đôi
            var room = new Room(BoardSize, opponent, player);
            _ = room.RunAsync();
            break;
        }
    }
    catch
    {
        // ignore lỗi
    }
}

sealed class PlayerConn
{
    public TcpClient Client { get; }
    public StreamReader Reader { get; }
    public StreamWriter Writer { get; }
    public string Nick { get; }
    public char Mark { get; set; } = '?';

    public PlayerConn(TcpClient c, StreamReader r, StreamWriter w, string nick)
    {
        Client = c;
        Reader = r;
        Writer = w;
        Nick = nick;
    }

    public Task SendAsync(string msg) => Writer.WriteLineAsync(msg);
}

sealed class Room
{
    private readonly int size;
    private readonly char[,] board;
    private readonly PlayerConn xPlayer;
    private readonly PlayerConn oPlayer;
    private PlayerConn current;
    private bool gameOver = false;

    public Room(int size, PlayerConn p1, PlayerConn p2)
    {
        this.size = size;
        board = new char[size, size];

        xPlayer = p1;
        oPlayer = p2;
        xPlayer.Mark = 'X';
        oPlayer.Mark = 'O';
        current = xPlayer;
    }

    public async Task RunAsync()
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"Phòng mới: {xPlayer.Nick}(X) vs {oPlayer.Nick}(O)");
        Console.ResetColor();

        try
        {
            await xPlayer.SendAsync("ROLE:X");
            await oPlayer.SendAsync("ROLE:O");

            await xPlayer.SendAsync($"OPPONENT:{oPlayer.Nick}");
            await oPlayer.SendAsync($"OPPONENT:{xPlayer.Nick}");

            await Broadcast($"START:SIZE={size}");
            await Broadcast($"TURN:{current.Mark}");

            var xLoop = ListenPlayer(xPlayer);
            var oLoop = ListenPlayer(oPlayer);

            await Task.WhenAny(xLoop, oLoop);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Lỗi phòng: {ex.Message}");
        }
        finally
        {
            Console.WriteLine($"Kết thúc trận: {xPlayer.Nick} vs {oPlayer.Nick}");
            
        }
    }
