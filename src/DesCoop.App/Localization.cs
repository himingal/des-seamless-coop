namespace DesCoop.App;

/// <summary>Tiny built-in localization for the launcher UI. Add a language by adding a column below.</summary>
public static class Loc
{
    public static readonly (string Code, string Name)[] Languages =
        [("en", "English"), ("pt", "Português (BR)"), ("es", "Español")];

    public static string Lang { get; set; } = "en";

    public static string T(string key) =>
        _table.TryGetValue(key, out var row) && row.TryGetValue(Lang, out var s) ? s
        : row != null && row.TryGetValue("en", out var e) ? e : key;

    static readonly Dictionary<string, Dictionary<string, string>> _table = new()
    {
        ["subtitle"] = new() {
            ["en"] = "Join your friend's world on RPCS3 and stay together for as long as you want.",
            ["pt"] = "Entre no mundo do seu amigo no RPCS3 e fiquem juntos o quanto quiserem.",
            ["es"] = "Entra al mundo de tu amigo en RPCS3 y quédense juntos todo el tiempo que quieran." },
        ["lblRpcn"] = new() { ["en"] = "RPCN Account", ["pt"] = "Conta RPCN", ["es"] = "Cuenta RPCN" },
        ["download"] = new() { ["en"] = "Download", ["pt"] = "Baixar", ["es"] = "Descargar" },
        ["update"] = new() { ["en"] = "Update", ["pt"] = "Atualizar", ["es"] = "Actualizar" },
        ["browse"] = new() { ["en"] = "Browse", ["pt"] = "Procurar", ["es"] = "Explorar" },
        ["install"] = new() { ["en"] = "Install", ["pt"] = "Instalar", ["es"] = "Instalar" },
        ["rpcnBtn"] = new() { ["en"] = "Create / Sign in", ["pt"] = "Criar / Entrar", ["es"] = "Crear / Entrar" },
        ["host"] = new() { ["en"] = "HOST A PARTY", ["pt"] = "CRIAR PARTY", ["es"] = "CREAR PARTY" },
        ["join"] = new() { ["en"] = "JOIN A PARTY", ["pt"] = "ENTRAR NUMA PARTY", ["es"] = "UNIRSE A PARTY" },
        ["public"] = new() { ["en"] = "PUBLIC SERVER", ["pt"] = "SERVIDOR PÚBLICO", ["es"] = "SERVIDOR PÚBLICO" },
        ["noneHint"] = new() {
            ["en"] = "One of you hosts, the other joins with the host's code.",
            ["pt"] = "Um de vocês hospeda, o outro entra com o código do host.",
            ["es"] = "Uno hospeda, el otro entra con el código del host." },
        ["partyName"] = new() { ["en"] = "Party name", ["pt"] = "Nome da party", ["es"] = "Nombre de la party" },
        ["password"] = new() { ["en"] = "Password", ["pt"] = "Senha", ["es"] = "Contraseña" },
        ["createParty"] = new() { ["en"] = "Create Party", ["pt"] = "Criar Party", ["es"] = "Crear Party" },
        ["closeParty"] = new() { ["en"] = "Close Party", ["pt"] = "Fechar Party", ["es"] = "Cerrar Party" },
        ["upnp"] = new() {
            ["en"] = "Also try to open router ports (UPnP)",
            ["pt"] = "Também tentar abrir portas do roteador (UPnP)",
            ["es"] = "También intentar abrir puertos del router (UPnP)" },
        ["worldTendency"] = new() { ["en"] = "World tendency", ["pt"] = "Tendência de mundo", ["es"] = "Tendencia del mundo" },
        ["tellFriend"] = new() { ["en"] = "Tell your friend:", ["pt"] = "Passe pro seu amigo:", ["es"] = "Dile a tu amigo:" },
        ["copy"] = new() { ["en"] = "Copy", ["pt"] = "Copiar", ["es"] = "Copiar" },
        ["joinName"] = new() { ["en"] = "Host's party name", ["pt"] = "Nome da party do host", ["es"] = "Nombre de la party del host" },
        ["joinBtn"] = new() { ["en"] = "Join", ["pt"] = "Entrar", ["es"] = "Entrar" },
        ["joinInfo"] = new() {
            ["en"] = "No VPN or port forwarding needed: the app finds the host and connects by itself.",
            ["pt"] = "Sem VPN nem abrir portas: o app acha o host e conecta sozinho.",
            ["es"] = "Sin VPN ni abrir puertos: la app encuentra al host y conecta sola." },
        ["publicInfo"] = new() {
            ["en"] = "Play on The Archstones community server: messages, ghosts and summon signs from players all over the world.",
            ["pt"] = "Jogue no servidor da comunidade The Archstones: mensagens, fantasmas e sinais de invocação de jogadores do mundo todo.",
            ["es"] = "Juega en el servidor comunitario The Archstones: mensajes, fantasmas y señales de invocación de jugadores de todo el mundo." },
        ["phantoms"] = new() { ["en"] = "PHANTOMS IN THIS WORLD", ["pt"] = "FANTASMAS NESTE MUNDO", ["es"] = "FANTASMAS EN ESTE MUNDO" },
        ["noPlayers"] = new() {
            ["en"] = "No one yet. Players show up here once the game goes online.",
            ["pt"] = "Ninguém ainda. Os jogadores aparecem aqui quando o jogo fica online.",
            ["es"] = "Nadie aún. Los jugadores aparecen aquí cuando el juego se conecta." },
        ["howTo"] = new() { ["en"] = "HOW TO PLAY TOGETHER", ["pt"] = "COMO JOGAR JUNTOS", ["es"] = "CÓMO JUGAR JUNTOS" },
        ["howToLines"] = new() {
            ["en"] = "I.   Both press PLAY. Online turns on by itself.\nII.  The helper dies once, then uses the Blue Eye Stone.\nIII. The sign appears right next to the host. Touch it.\nIV.  Boss down? Do it again and keep going.",
            ["pt"] = "I.   Os dois apertam PLAY. O online liga sozinho.\nII.  O ajudante morre uma vez e usa a Blue Eye Stone.\nIII. O sinal aparece do lado do host. Toque nele.\nIV.  Matou o chefe? Repita e continue.",
            ["es"] = "I.   Ambos pulsan PLAY. El online se activa solo.\nII.  El ayudante muere una vez y usa la Blue Eye Stone.\nIII. La señal aparece junto al host. Tócala.\nIV.  ¿Jefe muerto? Repite y sigan." },
        ["gameClosed"] = new() { ["en"] = "Demon's Souls is closed", ["pt"] = "Demon's Souls está fechado", ["es"] = "Demon's Souls está cerrado" },
        ["gameRunning"] = new() { ["en"] = "Demon's Souls is running", ["pt"] = "Demon's Souls está aberto", ["es"] = "Demon's Souls está abierto" },
        ["ready"] = new() { ["en"] = "Ready.", ["pt"] = "Pronto.", ["es"] = "Listo." },
        ["language"] = new() { ["en"] = "Language", ["pt"] = "Idioma", ["es"] = "Idioma" },
        ["pickLanguage"] = new() { ["en"] = "Choose your language:", ["pt"] = "Escolha seu idioma:", ["es"] = "Elige tu idioma:" },
    };
}
