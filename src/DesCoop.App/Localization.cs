namespace DesCoop.App;

/// <summary>Tiny built-in localization for the launcher UI. Add a language by adding a column below.</summary>
public static class Loc
{
    // A record (not a value tuple) so WPF can bind DisplayMemberPath="Name" / SelectedValuePath="Code".
    public sealed record LangOption(string Code, string Name);

    public static readonly LangOption[] Languages =
        [new("en", "English"), new("pt", "Português (BR)"), new("es", "Español")];

    public static string Lang { get; set; } = "en";

    public static string T(string key) =>
        _table.TryGetValue(key, out var row) && row.TryGetValue(Lang, out var s) ? s
        : row != null && row.TryGetValue("en", out var e) ? e : key;

    /// <summary>Localized string with <c>string.Format</c> arguments (placeholders {0}, {1}, …).</summary>
    public static string T(string key, params object[] args)
    {
        var s = T(key);
        try { return args.Length == 0 ? s : string.Format(s, args); }
        catch { return s; }
    }

    static readonly Dictionary<string, Dictionary<string, string>> _table = new()
    {
        ["subtitle"] = new() {
            ["en"] = "Join your friend's world on RPCS3 and stay together for as long as you want.",
            ["pt"] = "Entre no mundo do seu amigo no RPCS3 e fiquem juntos o quanto quiserem.",
            ["es"] = "Entra al mundo de tu amigo en RPCS3 y quédense juntos todo el tiempo que quieran." },

        // ---- Section headers -------------------------------------------------
        ["theNexus"] = new() { ["en"] = "THE NEXUS", ["pt"] = "O NEXUS", ["es"] = "EL NEXUS" },
        ["yourParty"] = new() { ["en"] = "YOUR PARTY", ["pt"] = "SUA PARTY", ["es"] = "TU PARTY" },

        // ---- Setup rows ------------------------------------------------------
        ["rpcs3Emulator"] = new() { ["en"] = "RPCS3 Emulator", ["pt"] = "Emulador RPCS3", ["es"] = "Emulador RPCS3" },
        ["ps3Firmware"] = new() { ["en"] = "PS3 Firmware", ["pt"] = "Firmware do PS3", ["es"] = "Firmware de PS3" },
        ["rpcs3Missing"] = new() {
            ["en"] = "Download RPCS3 from rpcs3.net, then point the app to its folder with Browse.",
            ["pt"] = "Baixe o RPCS3 em rpcs3.net e aponte o app para a pasta dele em Procurar.",
            ["es"] = "Descarga RPCS3 de rpcs3.net y apunta la app a su carpeta con Explorar." },
        ["getRpcs3"] = new() { ["en"] = "Get RPCS3", ["pt"] = "Baixar RPCS3", ["es"] = "Obtener RPCS3" },
        ["rpcs3Tools"] = new() { ["en"] = "RPCS3 TOOLS", ["pt"] = "FERRAMENTAS DO RPCS3", ["es"] = "HERRAMIENTAS DE RPCS3" },
        ["openRpcs3"] = new() { ["en"] = "Open RPCS3", ["pt"] = "Abrir RPCS3", ["es"] = "Abrir RPCS3" },
        ["gamepadHint"] = new() { ["en"] = "Tip: set your controller up in RPCS3 — Config › Gamepads.", ["pt"] = "Dica: configure seu controle no RPCS3 — Config › Gamepads.", ["es"] = "Consejo: configura tu mando en RPCS3 — Config › Gamepads." },
        ["fps60Off"] = new() { ["en"] = "60 FPS: Off", ["pt"] = "60 FPS: Desl.", ["es"] = "60 FPS: No" },
        ["fps60On"] = new() { ["en"] = "60 FPS: On", ["pt"] = "60 FPS: Lig.", ["es"] = "60 FPS: Sí" },
        ["st60Fps"] = new() {
            ["en"] = "Applying the 60 FPS patch…",
            ["pt"] = "Aplicando o patch de 60 FPS…",
            ["es"] = "Aplicando el parche de 60 FPS…" },
        ["fwInstalled"] = new() { ["en"] = "Installed.", ["pt"] = "Instalado.", ["es"] = "Instalado." },
        ["fwDownload"] = new() {
            ["en"] = "Downloads the official firmware straight from Sony.",
            ["pt"] = "Baixa o firmware oficial direto da Sony.",
            ["es"] = "Descarga el firmware oficial directo de Sony." },
        ["fwNeedRpcs3"] = new() { ["en"] = "Install RPCS3 first.", ["pt"] = "Instale o RPCS3 primeiro.", ["es"] = "Instala RPCS3 primero." },
        ["gameHint"] = new() {
            ["en"] = "Your own game dump: the folder that contains PS3_GAME.",
            ["pt"] = "Seu próprio dump do jogo: a pasta que contém PS3_GAME.",
            ["es"] = "Tu propio volcado del juego: la carpeta que contiene PS3_GAME." },
        ["gameNotDeS"] = new() {
            ["en"] = "\nThis doesn't look like Demon's Souls.",
            ["pt"] = "\nIsto não parece ser Demon's Souls.",
            ["es"] = "\nEsto no parece ser Demon's Souls." },
        ["rpcnSignedIn"] = new() { ["en"] = "Signed in as {0}.", ["pt"] = "Conectado como {0}.", ["es"] = "Conectado como {0}." },

        ["lblRpcn"] = new() { ["en"] = "RPCN Account", ["pt"] = "Conta RPCN", ["es"] = "Cuenta RPCN" },
        ["download"] = new() { ["en"] = "Download", ["pt"] = "Baixar", ["es"] = "Descargar" },
        ["update"] = new() { ["en"] = "Update", ["pt"] = "Atualizar", ["es"] = "Actualizar" },
        ["browse"] = new() { ["en"] = "Browse", ["pt"] = "Procurar", ["es"] = "Explorar" },
        ["install"] = new() { ["en"] = "Install", ["pt"] = "Instalar", ["es"] = "Instalar" },
        ["change"] = new() { ["en"] = "Change", ["pt"] = "Alterar", ["es"] = "Cambiar" },
        ["rpcnBtn"] = new() { ["en"] = "Create / Sign in", ["pt"] = "Criar / Entrar", ["es"] = "Crear / Entrar" },
        ["rpcnHint"] = new() {
            ["en"] = "Free account needed for online play (RPCS3's PlayStation Network).",
            ["pt"] = "Conta grátis necessária para jogar online (a PlayStation Network do RPCS3).",
            ["es"] = "Cuenta gratis necesaria para jugar en línea (la PlayStation Network de RPCS3)." },

        // ---- Party mode ------------------------------------------------------
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
        ["wtPureWhite"] = new() { ["en"] = "PURE WHITE", ["pt"] = "BRANCO PURO", ["es"] = "BLANCO PURO" },
        ["wtWhite"] = new() { ["en"] = "WHITE", ["pt"] = "BRANCO", ["es"] = "BLANCO" },
        ["wtNormal"] = new() { ["en"] = "NORMAL", ["pt"] = "NORMAL", ["es"] = "NORMAL" },
        ["wtBlack"] = new() { ["en"] = "BLACK", ["pt"] = "PRETO", ["es"] = "NEGRO" },
        ["wtPureBlack"] = new() { ["en"] = "PURE BLACK", ["pt"] = "PRETO PURO", ["es"] = "NEGRO PURO" },
        ["tellFriend"] = new() { ["en"] = "Tell your friend:", ["pt"] = "Passe pro seu amigo:", ["es"] = "Dile a tu amigo:" },
        ["copy"] = new() { ["en"] = "Copy", ["pt"] = "Copiar", ["es"] = "Copiar" },
        ["invite"] = new() {
            ["en"] = "Party: {0}     Password: {1}",
            ["pt"] = "Party: {0}     Senha: {1}",
            ["es"] = "Party: {0}     Contraseña: {1}" },
        ["joinName"] = new() { ["en"] = "Host's party name", ["pt"] = "Nome da party do host", ["es"] = "Nombre de la party del host" },
        ["joinBtn"] = new() { ["en"] = "Join", ["pt"] = "Entrar", ["es"] = "Entrar" },
        ["joinInfo"] = new() {
            ["en"] = "No VPN or port forwarding needed: the app finds the host and connects by itself.",
            ["pt"] = "Sem VPN nem abrir portas: o app acha o host e conecta sozinho.",
            ["es"] = "Sin VPN ni abrir puertos: la app encuentra al host y conecta sola." },
        ["joinLooking"] = new() { ["en"] = "Looking for the party…", ["pt"] = "Procurando a party…", ["es"] = "Buscando la party…" },
        ["joinNeedName"] = new() {
            ["en"] = "Type the host's party name and password.",
            ["pt"] = "Digite o nome e a senha da party do host.",
            ["es"] = "Escribe el nombre y la contraseña de la party del host." },
        ["joinFailed"] = new() { ["en"] = "Could not join.", ["pt"] = "Não foi possível entrar.", ["es"] = "No se pudo entrar." },
        ["viaRelay"] = new() { ["en"] = " through the relay", ["pt"] = " pelo relay", ["es"] = " por el relay" },
        ["viaAddr"] = new() { ["en"] = " via {0}", ["pt"] = " via {0}", ["es"] = " vía {0}" },
        ["joinConnected"] = new() {
            ["en"] = "Connected to \"{0}\"{1}. Now press PLAY and keep this app open.",
            ["pt"] = "Conectado a \"{0}\"{1}. Agora aperte PLAY e mantenha este app aberto.",
            ["es"] = "Conectado a \"{0}\"{1}. Ahora pulsa PLAY y mantén esta app abierta." },
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
            ["en"] = "I.   Both press PLAY. Online turns on by itself.\nII.  Helper: use the Join Sigil, in any form - you enter by yourself.\nIII. Host: use the Host Sigil - your friend joins by himself (Nexus too).\nIV.  Bosses, treasure and NPCs count for both - and you stay together.",
            ["pt"] = "I.   Os dois apertam PLAY. O online liga sozinho.\nII.  Ajudante: use o Join Sigil, em qualquer forma - entra sozinho.\nIII. Host: use o Host Sigil - o amigo entra sozinho (até no Nexus).\nIV.  Chefes, baús e NPCs valem pros dois - e vocês continuam juntos.",
            ["es"] = "I.   Ambos pulsan PLAY. El online se activa solo.\nII.  Ayudante: usa el Join Sigil, en cualquier forma - entras solo.\nIII. Host: usa el Host Sigil - tu amigo entra solo (también en el Nexus).\nIV.  Jefes, tesoros y NPCs cuentan para ambos - y siguen juntos." },

        // ---- Host info panel -------------------------------------------------
        ["relayOn"] = new() {
            ["en"] = "Relay online: your friend can join from anywhere, no VPN needed.",
            ["pt"] = "Relay online: seu amigo pode entrar de qualquer lugar, sem VPN.",
            ["es"] = "Relay en línea: tu amigo puede entrar desde cualquier lugar, sin VPN." },
        ["relayOff"] = new() {
            ["en"] = "Relay offline: only LAN/VPN/open-port connections will work.",
            ["pt"] = "Relay offline: só conexões por LAN/VPN/porta aberta vão funcionar.",
            ["es"] = "Relay sin conexión: solo funcionarán conexiones por LAN/VPN/puerto abierto." },
        ["listed"] = new() {
            ["en"] = "Party listed: your friend types the name and password in \"Join a Party\".",
            ["pt"] = "Party listada: seu amigo digita o nome e a senha em \"Entrar numa Party\".",
            ["es"] = "Party publicada: tu amigo escribe el nombre y la contraseña en \"Unirse a Party\"." },
        ["notListed"] = new() { ["en"] = "Party not listed online yet.", ["pt"] = "Party ainda não listada online.", ["es"] = "La party aún no está publicada." },
        ["upnpOpened"] = new() {
            ["en"] = "Router ports opened via UPnP ({0}).",
            ["pt"] = "Portas do roteador abertas via UPnP ({0}).",
            ["es"] = "Puertos del router abiertos vía UPnP ({0})." },
        ["keepOpenServer"] = new() {
            ["en"] = "Keep this app open while you play: it is the party server.",
            ["pt"] = "Mantenha este app aberto enquanto joga: ele é o servidor da party.",
            ["es"] = "Mantén esta app abierta mientras juegas: es el servidor de la party." },

        // ---- Status line -----------------------------------------------------
        ["ready"] = new() { ["en"] = "Ready.", ["pt"] = "Pronto.", ["es"] = "Listo." },
        ["stDlRpcs3"] = new() { ["en"] = "Downloading RPCS3…", ["pt"] = "Baixando o RPCS3…", ["es"] = "Descargando RPCS3…" },
        ["stInstallFw"] = new() { ["en"] = "Installing firmware…", ["pt"] = "Instalando o firmware…", ["es"] = "Instalando el firmware…" },
        ["stCreatingParty"] = new() { ["en"] = "Creating party…", ["pt"] = "Criando a party…", ["es"] = "Creando la party…" },
        ["stOpeningParty"] = new() { ["en"] = "Opening your party…", ["pt"] = "Abrindo sua party…", ["es"] = "Abriendo tu party…" },
        ["stRejoining"] = new() { ["en"] = "Rejoining the party…", ["pt"] = "Reentrando na party…", ["es"] = "Reingresando a la party…" },
        ["stPreparing"] = new() { ["en"] = "Preparing…", ["pt"] = "Preparando…", ["es"] = "Preparando…" },
        ["stConfiguring"] = new() { ["en"] = "Configuring RPCS3…", ["pt"] = "Configurando o RPCS3…", ["es"] = "Configurando RPCS3…" },
        ["stPatch"] = new() { ["en"] = "Checking the co-op patch…", ["pt"] = "Verificando o patch de co-op…", ["es"] = "Comprobando el parche de co-op…" },
        ["stFirewall"] = new() {
            ["en"] = "Allowing through Windows Firewall (accept the Windows prompt)…",
            ["pt"] = "Liberando no Firewall do Windows (aceite o aviso do Windows)…",
            ["es"] = "Permitiendo en el Firewall de Windows (acepta el aviso de Windows)…" },
        ["stCopied"] = new() {
            ["en"] = "Copied. Send it to your friend (Discord, WhatsApp…).",
            ["pt"] = "Copiado. Envie pro seu amigo (Discord, WhatsApp…).",
            ["es"] = "Copiado. Envíaselo a tu amigo (Discord, WhatsApp…)." },
        ["stGameHost"] = new() {
            ["en"] = "Game running. Keep this app open: it is the party server.",
            ["pt"] = "Jogo aberto. Mantenha este app aberto: ele é o servidor da party.",
            ["es"] = "Juego en marcha. Mantén esta app abierta: es el servidor de la party." },
        ["stGameSolo"] = new() {
            ["en"] = "Game running. Good hunting, Slayer of Demons.",
            ["pt"] = "Jogo aberto. Boa caçada, Matador de Demônios.",
            ["es"] = "Juego en marcha. Buena cacería, Cazador de Demonios." },
        ["stError"] = new() { ["en"] = "Error: {0}", ["pt"] = "Erro: {0}", ["es"] = "Error: {0}" },

        // ---- Dialogs (message boxes) ----------------------------------------
        ["dgNeedRpcs3"] = new() { ["en"] = "Install RPCS3 first (Download).", ["pt"] = "Instale o RPCS3 primeiro (Baixar).", ["es"] = "Instala RPCS3 primero (Descargar)." },
        ["dgNeedFw"] = new() { ["en"] = "Install the PS3 firmware first.", ["pt"] = "Instale o firmware do PS3 primeiro.", ["es"] = "Instala el firmware de PS3 primero." },
        ["dgNeedGame"] = new() { ["en"] = "Pick your Demon's Souls folder first.", ["pt"] = "Escolha a pasta do Demon's Souls primeiro.", ["es"] = "Elige la carpeta de Demon's Souls primero." },
        ["dgNeedMode"] = new() {
            ["en"] = "Choose one: Host a Party, Join a Party or Public Server.",
            ["pt"] = "Escolha uma: Criar Party, Entrar numa Party ou Servidor Público.",
            ["es"] = "Elige una: Crear Party, Unirse a Party o Servidor Público." },
        ["dgOfflineAnyway"] = new() {
            ["en"] = "No RPCN account yet, so online co-op won't work.\n\nPlay offline anyway?",
            ["pt"] = "Ainda sem conta RPCN, então o co-op online não vai funcionar.\n\nJogar offline mesmo assim?",
            ["es"] = "Aún sin cuenta RPCN, así que el co-op en línea no funcionará.\n\n¿Jugar sin conexión de todas formas?" },
        ["dgRpcs3Running"] = new() {
            ["en"] = "RPCS3 is already open. Close it so the app can apply the network settings before launching.",
            ["pt"] = "O RPCS3 já está aberto. Feche-o para o app aplicar as configurações de rede antes de iniciar.",
            ["es"] = "RPCS3 ya está abierto. Ciérralo para que la app aplique la configuración de red antes de iniciar." },
        ["dgCloseRpcs3"] = new() { ["en"] = "Close RPCS3 first.", ["pt"] = "Feche o RPCS3 primeiro.", ["es"] = "Cierra RPCS3 primero." },
        ["dgNoRpcs3Exe"] = new() {
            ["en"] = "rpcs3.exe was not found in that folder.",
            ["pt"] = "rpcs3.exe não foi encontrado nessa pasta.",
            ["es"] = "rpcs3.exe no se encontró en esa carpeta." },
        ["dgNoGame"] = new() {
            ["en"] = "PS3_GAME/USRDIR/EBOOT.BIN was not found there.\nPick your game dump folder (e.g. BLUS30443).",
            ["pt"] = "PS3_GAME/USRDIR/EBOOT.BIN não foi encontrado ali.\nEscolha a pasta do dump do jogo (ex.: BLUS30443).",
            ["es"] = "PS3_GAME/USRDIR/EBOOT.BIN no se encontró ahí.\nElige la carpeta del volcado del juego (p. ej. BLUS30443)." },
        ["dgTitleRpcs3"] = new() { ["en"] = "Folder that contains rpcs3.exe", ["pt"] = "Pasta que contém rpcs3.exe", ["es"] = "Carpeta que contiene rpcs3.exe" },
        ["dgTitleGame"] = new() {
            ["en"] = "Demon's Souls folder (the one that contains PS3_GAME)",
            ["pt"] = "Pasta do Demon's Souls (a que contém PS3_GAME)",
            ["es"] = "Carpeta de Demon's Souls (la que contiene PS3_GAME)" },
        ["dgHostClose"] = new() {
            ["en"] = "You are the host. Closing the app ends the party for your friend.\n\nClose anyway?",
            ["pt"] = "Você é o host. Fechar o app encerra a party pro seu amigo.\n\nFechar mesmo assim?",
            ["es"] = "Eres el host. Cerrar la app termina la party para tu amigo.\n\n¿Cerrar de todas formas?" },
        ["dgRelayClose"] = new() {
            ["en"] = "You are connected through the relay. Closing the app disconnects you from the party.\n\nClose anyway?",
            ["pt"] = "Você está conectado pelo relay. Fechar o app te desconecta da party.\n\nFechar mesmo assim?",
            ["es"] = "Estás conectado por el relay. Cerrar la app te desconecta de la party.\n\n¿Cerrar de todas formas?" },

        // ---- RPCN window -----------------------------------------------------
        ["rpcnTitle"] = new() { ["en"] = "RPCN ACCOUNT", ["pt"] = "CONTA RPCN", ["es"] = "CUENTA RPCN" },
        ["rpcnBlurb"] = new() {
            ["en"] = "RPCN is RPCS3's free PlayStation Network. You and your friend each need one to play online.",
            ["pt"] = "RPCN é a PlayStation Network grátis do RPCS3. Você e seu amigo precisam de uma conta cada para jogar online.",
            ["es"] = "RPCN es la PlayStation Network gratuita de RPCS3. Tú y tu amigo necesitan una cuenta cada uno para jugar en línea." },
        ["rpcnCreate"] = new() { ["en"] = "CREATE", ["pt"] = "CRIAR", ["es"] = "CREAR" },
        ["rpcnHaveOne"] = new() { ["en"] = "I HAVE ONE", ["pt"] = "JÁ TENHO", ["es"] = "YA TENGO" },
        ["rpcnUser"] = new() {
            ["en"] = "Username (3-16 letters, numbers, - or _)",
            ["pt"] = "Usuário (3-16 letras, números, - ou _)",
            ["es"] = "Usuario (3-16 letras, números, - o _)" },
        ["rpcnEmail"] = new() {
            ["en"] = "Email (a 16-letter token is sent there)",
            ["pt"] = "E-mail (um token de 16 letras é enviado pra lá)",
            ["es"] = "Email (allí se envía un token de 16 letras)" },
        ["rpcnPass2"] = new() { ["en"] = "Password again", ["pt"] = "Senha de novo", ["es"] = "Contraseña otra vez" },
        ["rpcnTokenSignIn"] = new() {
            ["en"] = "Token from the RPCN email",
            ["pt"] = "Token do e-mail do RPCN",
            ["es"] = "Token del email de RPCN" },
        ["rpcnCreateAccount"] = new() { ["en"] = "Create Account", ["pt"] = "Criar Conta", ["es"] = "Crear Cuenta" },
        ["rpcnSignInBtn"] = new() { ["en"] = "Sign In", ["pt"] = "Entrar", ["es"] = "Entrar" },
        ["rpcnCreated"] = new() { ["en"] = "Account created", ["pt"] = "Conta criada", ["es"] = "Cuenta creada" },
        ["rpcnPasteToken"] = new() {
            ["en"] = "Open your email (check spam too) and paste the 16-letter token from RPCN here.",
            ["pt"] = "Abra seu e-mail (veja o spam também) e cole aqui o token de 16 letras do RPCN.",
            ["es"] = "Abre tu email (revisa el spam también) y pega aquí el token de 16 letras de RPCN." },
        ["rpcnBack"] = new() { ["en"] = "Back", ["pt"] = "Voltar", ["es"] = "Atrás" },
        ["rpcnResend"] = new() { ["en"] = "Resend Email", ["pt"] = "Reenviar E-mail", ["es"] = "Reenviar Email" },
        ["rpcnConfirm"] = new() { ["en"] = "Confirm", ["pt"] = "Confirmar", ["es"] = "Confirmar" },
        ["rpcnVUser"] = new() {
            ["en"] = "Username must be 3-16 letters, numbers, - or _.",
            ["pt"] = "O usuário deve ter 3-16 letras, números, - ou _.",
            ["es"] = "El usuario debe tener 3-16 letras, números, - o _." },
        ["rpcnVPass"] = new() { ["en"] = "Pick a longer password.", ["pt"] = "Escolha uma senha mais longa.", ["es"] = "Elige una contraseña más larga." },
        ["rpcnVEmail"] = new() {
            ["en"] = "Enter a real email: RPCN sends the token there.",
            ["pt"] = "Digite um e-mail real: o RPCN envia o token pra lá.",
            ["es"] = "Escribe un email real: RPCN envía el token allí." },
        ["rpcnVMatch"] = new() { ["en"] = "Passwords don't match.", ["pt"] = "As senhas não conferem.", ["es"] = "Las contraseñas no coinciden." },
        ["rpcnVToken"] = new() {
            ["en"] = "The token is 16 characters (A-Z, 0-9).",
            ["pt"] = "O token tem 16 caracteres (A-Z, 0-9).",
            ["es"] = "El token tiene 16 caracteres (A-Z, 0-9)." },
        ["rpcnTalking"] = new() { ["en"] = "Talking to RPCN…", ["pt"] = "Falando com o RPCN…", ["es"] = "Hablando con RPCN…" },
        ["rpcnCheckToken"] = new() { ["en"] = "Checking the token…", ["pt"] = "Verificando o token…", ["es"] = "Comprobando el token…" },
        ["rpcnResending"] = new() {
            ["en"] = "Asking RPCN to resend the email…",
            ["pt"] = "Pedindo pro RPCN reenviar o e-mail…",
            ["es"] = "Pidiendo a RPCN que reenvíe el email…" },
        ["rpcnResent"] = new() {
            ["en"] = "Sent. Check your inbox and spam folder.",
            ["pt"] = "Enviado. Veja sua caixa de entrada e o spam.",
            ["es"] = "Enviado. Revisa tu bandeja de entrada y el spam." },
        ["rpcnUnreachable"] = new() { ["en"] = "Could not reach RPCN: {0}", ["pt"] = "Não deu para acessar o RPCN: {0}", ["es"] = "No se pudo contactar con RPCN: {0}" },
        ["rpcnDone"] = new() {
            ["en"] = "You're in, {0}. RPCS3 will sign in by itself when the game goes online.",
            ["pt"] = "Você entrou, {0}. O RPCS3 conecta sozinho quando o jogo ficar online.",
            ["es"] = "Ya estás, {0}. RPCS3 iniciará sesión solo cuando el juego se conecte." },

        // ---- Setup window (runs right after install) ------------------------
        ["suTitlePreparing"] = new() { ["en"] = "PREPARING THE NEXUS", ["pt"] = "PREPARANDO O NEXUS", ["es"] = "PREPARANDO EL NEXUS" },
        ["suTitleAlmost"] = new() { ["en"] = "ALMOST THERE", ["pt"] = "QUASE LÁ", ["es"] = "CASI LISTO" },
        ["suTitleDone"] = new() { ["en"] = "THE NEXUS AWAITS", ["pt"] = "O NEXUS AGUARDA", ["es"] = "EL NEXUS AGUARDA" },
        ["suStarting"] = new() { ["en"] = "Starting…", ["pt"] = "Começando…", ["es"] = "Empezando…" },
        ["suAllSet"] = new() { ["en"] = "All set.", ["pt"] = "Tudo pronto.", ["es"] = "Todo listo." },
        ["suSomeFailed"] = new() {
            ["en"] = "Some steps failed (no internet?). The app has buttons to retry each one.",
            ["pt"] = "Algumas etapas falharam (sem internet?). O app tem botões para refazer cada uma.",
            ["es"] = "Algunos pasos fallaron (¿sin internet?). La app tiene botones para reintentar cada uno." },
        ["suContinue"] = new() { ["en"] = "Continue", ["pt"] = "Continuar", ["es"] = "Continuar" },
        ["suPatching"] = new() {
            ["en"] = "Patching the game files (backup kept)…",
            ["pt"] = "Aplicando o patch nos arquivos do jogo (backup mantido)…",
            ["es"] = "Aplicando el parche a los archivos del juego (se guarda copia)…" },
        ["suStepRpcs3"] = new() { ["en"] = "RPCS3 emulator", ["pt"] = "Emulador RPCS3", ["es"] = "Emulador RPCS3" },
        ["suStepFw"] = new() { ["en"] = "PS3 firmware (from Sony)", ["pt"] = "Firmware do PS3 (da Sony)", ["es"] = "Firmware de PS3 (de Sony)" },
        ["suStepGame"] = new() { ["en"] = "Demon's Souls", ["pt"] = "Demon's Souls", ["es"] = "Demon's Souls" },
        ["suStepPatch"] = new() { ["en"] = "Co-op patch", ["pt"] = "Patch de co-op", ["es"] = "Parche de co-op" },
        ["suStepSettings"] = new() { ["en"] = "RPCS3 settings", ["pt"] = "Configurações do RPCS3", ["es"] = "Ajustes de RPCS3" },
        ["suWaiting"] = new() { ["en"] = "waiting", ["pt"] = "aguardando", ["es"] = "esperando" },
        ["suWorking"] = new() { ["en"] = "working…", ["pt"] = "trabalhando…", ["es"] = "trabajando…" },
        ["suFailed"] = new() { ["en"] = "failed", ["pt"] = "falhou", ["es"] = "falló" },
        ["suAlready"] = new() { ["en"] = "already installed", ["pt"] = "já instalado", ["es"] = "ya instalado" },
        ["suInstalled"] = new() { ["en"] = "installed", ["pt"] = "instalado", ["es"] = "instalado" },
        ["suSkipped"] = new() { ["en"] = "skipped", ["pt"] = "pulado", ["es"] = "omitido" },
        ["suDone"] = new() { ["en"] = "done", ["pt"] = "pronto", ["es"] = "listo" },
        ["suPickLater"] = new() {
            ["en"] = "pick it later in the app",
            ["pt"] = "escolha depois no app",
            ["es"] = "elígelo luego en la app" },
        ["suNotFound"] = new() {
            ["en"] = "not found, pick it in the app",
            ["pt"] = "não encontrado, escolha no app",
            ["es"] = "no encontrado, elígelo en la app" },
        ["suPartial"] = new() { ["en"] = "partially applied", ["pt"] = "aplicado em parte", ["es"] = "aplicado en parte" },
        ["suApplied"] = new() { ["en"] = "applied", ["pt"] = "aplicado", ["es"] = "aplicado" },

        ["language"] = new() { ["en"] = "Language", ["pt"] = "Idioma", ["es"] = "Idioma" },
        ["pickLanguage"] = new() { ["en"] = "Choose your language:", ["pt"] = "Escolha seu idioma:", ["es"] = "Elige tu idioma:" },
    };
}
