<p align="center"><img src="docs/banner.png" alt="DeS Seamless Co-op" width="720"></p>

# DeS Seamless Co-op (RPCS3)

Co-op "sem costura" de **Demon's Souls** no **RPCS3**: um cria a party, o outro cola o código, e vocês jogam juntos o tempo que quiserem.

## Instalar

1. Baixe o `DesSeamlessCoop-Setup.exe` em **[Releases](../../releases/latest)**, rode, escolha a pasta, pronto.
2. Abra o app e siga a coluna **Preparação** (tudo tem botão):
   - **RPCS3** → *Baixar* (baixa o RPCS3 oficial e o firmware oficial da Sony sozinho) ou *Já tenho…*
   - **Demon's Souls** → *Escolher pasta…* (seu próprio dump, a pasta que tem `PS3_GAME`)
   - **Conta RPCN** → *Criar / entrar* (abre o RPCS3: `Configuration › RPCN › Create Account`)

## Jogar

| Host | Amigo |
|---|---|
| **Criar party** → *Copiar* código → manda pro amigo | **Entrar na party** → cola o código → *Entrar* |
| **JOGAR** | **JOGAR** |

No jogo: quem ajuda usa a **Blue Eye Stone** em qualquer lugar. O sinal aparece **do lado do host, na área onde ele estiver**. Host toca no sinal → co-op. Depois do boss, Blue Eye Stone de novo.

Se o amigo não conseguir conectar: o roteador do host não tem UPnP ou é CGNAT. Instalem o **Radmin VPN** (grátis), entrem na mesma rede e o host cria a party de novo — o código passa a incluir o IP da VPN.

## O que ele faz

- **Servidor de party embutido** (reimplementação do servidor de Demon's Souls): o host roda dentro do app, sem VPS.
  - Sinais azuis de membros da party são **reposicionados ao lado do host em qualquer área** (usa a posição que o próprio jogo envia).
  - Sem limite de nível, regiões US/EU/JP juntas, mensagens, manchas de sangue e fantasmas funcionando.
  - Lista em tempo real de quem está online, em que área, com sinal ativo ou em co-op.
- **Patch no jogo** (aplicado no seu dump, com backup e botão *Original*):
  - Blue Eye Stone utilizável em **forma humana** → sem precisar morrer para voltar a ajudar.
  - Stone of Ephemeral Eyes **infinita** → host sempre consegue voltar ao corpo para invocar.
- **Configura o RPCS3 sozinho**: RPCN, redirecionamento dos servidores, UPnP, "Skip Intro", registro do jogo.
- **Modo servidor dedicado**: `DesCoop.exe --server --name "Minha Party"` (VPS / PC ligado 24h).
- **Servidor público**: um clique para jogar no *The Archstones* com o mundo todo.

## Limites (do jogo, não do app)

O jogo ainda encerra a sessão ao matar um boss ou quando o host morre — é código do executável do PS3. O app reduz isso a 2 cliques (Blue Eye Stone em forma humana + sinal que aparece do lado do host), mas não impede a desconexão.

## Compilar

```powershell
git clone --recursive https://github.com/himingal/des-seamless-coop
powershell -File tools/build-release.ps1 -Version 1.0.0   # testes + exe single-file + instalador em dist/
```

## Créditos

- Protocolo do servidor: [DeSSE](https://github.com/ymgve/desse) (ymgve) e [dessego](https://github.com/danmrichards/dessego)
- [RPCS3](https://rpcs3.net) e [RPCN](https://github.com/RipleyTom/rpcn)
- [SoulsFormatsNEXT](https://github.com/soulsmods/SoulsFormatsNEXT) (GPL-3.0) e paramdefs do [Paramdex](https://github.com/soulsmods/Paramdex)
- [The Archstones](https://thearchstones.com)

Licença GPL-3.0. Não inclui nenhum arquivo do jogo: use a sua cópia.
