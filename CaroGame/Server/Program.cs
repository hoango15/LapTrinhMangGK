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
    private async Task ListenPlayer(PlayerConn p)
    {
        string? line;
        while ((line = await p.Reader.ReadLineAsync()) != null)
        {
            if (line.StartsWith("MOVE:"))
            {
                if (gameOver)
                {
                    await p.SendAsync("INFO:Ván đã kết thúc, chờ rematch hoặc thoát.");
                    continue;
                }

                if (p != current)
                {
                    await p.SendAsync("INFO:Chưa tới lượt bạn!");
                    continue;
                }

                var payload = line[5..];
                if (!TryParseMove(payload, out int x, out int y))
                {
                    await p.SendAsync("INFO:Lỗi cú pháp MOVE:x,y");
                    continue;
                }

                if (!InBoard(x, y) || board[y, x] != '\0')
                {
                    await p.SendAsync("INFO:Ô không hợp lệ hoặc đã đánh rồi");
                    continue;
                }

                board[y, x] = p.Mark;
                await Broadcast($"BOARD:{x},{y},{p.Mark}");

                if (IsWin(x, y, p.Mark))
                {
                    gameOver = true;
                    await Broadcast($"WIN:{p.Mark}");
                    continue;
                }

                if (IsDraw())
                {
                    gameOver = true;
                    await Broadcast("DRAW");
                    continue;
                }

                current = (current == xPlayer) ? oPlayer : xPlayer;
                await Broadcast($"TURN:{current.Mark}");
            }
            else if (line == "REMATCH")
            {
                if (!gameOver)
                {
                    await p.SendAsync("INFO:Ván đang diễn ra, không thể rematch!");
                    continue;
                }

                await HandleRematch(p);
            }
            else if (line == "EXIT")
            {
                await p.SendAsync("INFO:Bạn đã thoát khỏi ván!");
                break;
            }
        }
    }

    private async Task HandleRematch(PlayerConn requester)
    {
        PlayerConn other = requester == xPlayer ? oPlayer : xPlayer;

        if (!gameOver)
            return;

        await requester.SendAsync("INFO:Đang chuẩn bị ván mới...");
        await other.SendAsync("INFO:Đối thủ muốn chơi lại...");

        // Reset bàn cờ
        Array.Clear(board, 0, board.Length);
        current = xPlayer;
        gameOver = false;

        await Broadcast($"START:SIZE={size}");
        await Broadcast($"TURN:{current.Mark}");
    }

    private static bool TryParseMove(string s, out int x, out int y)
    {
        x = y = -1;
        var parts = s.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2) return false;
        return int.TryParse(parts[0], out x) && int.TryParse(parts[1], out y);
    }

    private bool InBoard(int x, int y) => x >= 0 && y >= 0 && x < size && y < size;

    private async Task Broadcast(string msg)
    {
        await Task.WhenAll(xPlayer.SendAsync(msg), oPlayer.SendAsync(msg));
    }

    private bool IsDraw()
    {
        for (int r = 0; r < size; r++)
            for (int c = 0; c < size; c++)
                if (board[r, c] == '\0') return false;
        return true;
    }

    private bool IsWin(int x, int y, char mark)
    {
        return CountLine(x, y, 1, 0, mark) + CountLine(x, y, -1, 0, mark) - 1 >= 5 ||
               CountLine(x, y, 0, 1, mark) + CountLine(x, y, 0, -1, mark) - 1 >= 5 ||
               CountLine(x, y, 1, 1, mark) + CountLine(x, y, -1, -1, mark) - 1 >= 5 ||
               CountLine(x, y, 1, -1, mark) + CountLine(x, y, -1, 1, mark) - 1 >= 5;
    }

    private int CountLine(int x, int y, int dx, int dy, char mark)
    {
        int count = 0;
        while (InBoard(x, y) && board[y, x] == mark)
        {
            count++;
            x += dx;
            y += dy;
        }
        return count;
    }
}
