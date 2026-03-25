namespace ServerDistorApi.Model;

public class User
{
    public int Id { get; set; }
    public int IdServer { get; set; }
    public DateTime TimeArentServer { get; set; }

    public User()
    {
    }

    public User(int idServer, DateTime timeArentServer)
    {
        IdServer = idServer;
        TimeArentServer = timeArentServer;
    }
}