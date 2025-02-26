namespace Main
{
    public class PlayerInfo
    {
        public ulong CID;
        public string Name;
        public string HomeWorld;

        public PlayerInfo(ulong cid,string name,string homeWorld)
        {
            CID = cid;
            Name = name;
            HomeWorld = homeWorld;
        }
    }
}
