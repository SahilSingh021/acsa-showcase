namespace acsa_web.Services
{
    public static class PortStore
    {
        public static Dictionary<int, string> PortDict { get; private set; }

        public static void Initialize()
        {
            PortDict = FillOddPorts(55345, 55395);
        }

        private static Dictionary<int, string> FillOddPorts(int startPort, int endPort)
        {
            var ports = new Dictionary<int, string>();
            for (int port = startPort; port <= endPort; port++)
                if (port % 2 == 1)
                    ports[port] = "unused";
            return ports;
        }
    }
}
