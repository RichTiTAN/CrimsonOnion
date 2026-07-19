# CrimsonOnion
A GUI client that runs Multiple Tor instances and load-balances them with HAProxy.  
This project is a rewritten and improved version of [TorMultiplexer](https://github.com/richTiTAN/Tor-Multiplexer/) in C# and .NET.  

<img width="2900" height="1080" alt="coui" src="https://github.com/user-attachments/assets/662c1e32-dfad-483d-be5f-63c77fbb1165" />


__HOW IT WORKS:__
- This app uses 1-8 Tor connections, load-balances them with HAProxy, uses Xray-core for managing and enabling the Proxy and Sing-box for managing the VPN MODE.
- [FARSI GUIDE](https://github.com/RichTiTAN/CrimsonOnion/blob/main/readmeFA.md)
  
# How to use and Troubleshooting
The interface of the app is pretty simple and tooltips have been included to help with understanding each option. But a guide is provided below nonetheless.

__APP TAKES TOO LONG TO CONNECT:__
- Tor works by routing your traffic through a decentralized network of volunteer-operated relays. When connecting, Tor needs to download the directory of these relays and establish encrypted circuits. On slow networks, or if access to the Tor network is restricted, this bootstrap process might take a while. Please be patient, or try using a different Bridge Type if it gets stuck.

__UDP HANDLING:__
- The Tor network only natively supports TCP traffic. However, you can still handle UDP traffic in two ways using this app:
  1. Use the **Custom Xray Exit-Node** option to connect to an Xray config (VLESS, VMess, Trojan, SS) after Tor. Xray will encapsulate UDP traffic inside TCP (UDP over TCP), allowing it to pass through Tor successfully.
  2. Use the **Direct UDP** option in Split Tunneling to bypass Tor entirely for UDP traffic. This will route all UDP directly to your internet adapter, which is useful for video games or voice chat, but it reduces your anonymity.

__ROUTING/COUNTRIES:__  
- Recommended routing option as set by default is "**Optimized**". By selecting Optimized a series of recommended settings for your Tor configuration will be applied. This will potentially connect the Tor engines to different locations which may increase the chance of getting faced with a captcha whenever you enter a website.
- If you would rather have a single exit location (which may reduce the chance of facing captchas) you can choose your desired country from the list.
- The "**Expert**" option is either for people who want the barebone Tor experience with no adjustments or for people who want maximum flexibility with their Tor configurations. For the barebone experience you have to click "Expert" then click "Save" on the opened window.

__BRIDGE TYPE:__  
- This option will show you different bridge types you can use to connect to Tor.
- On healthy networks you can use "Direct".
- If Tor is blocked in your region you can choose other bridge types. Try with this order: Obfs4, Snowflake, meek_lite and see which one works better for you and your network interface.
- **Custom Bridges**: By opening the Custom Bridge menu you can request webtunnel or obfs4 bridges; these are known to work very well in restricted areas. Alternatively, if you can't fetch bridges from inside the app, you can manually grab custom bridges and paste them here.

__TOR ENGINES:__
- You can set the amount of Tor instances that you want to run and load-balance. Higher doesn't always mean faster; 6 is the recommended amount for use on a single device. You can go higher if you want to connect other devices to the app.

__LOGS AND STATUS:__  
- This option will open up a logs panel where you can monitor the Tor bootstrap status and Xray logs.

__STATS:__
- In this box you will see the connection time. It starts after you've established a connection.
- Download, Upload, Ping, Total, and the Speed graph will display your connection details.
- **LOCAL and LAN PORTS**: These options will display your current Local/LAN IP addresses and ports. You can copy them by clicking on them. By using these options you can configure your local device or other devices on your network to use Tor.

__CONNECTION MODES:__
- **PROXY MODE**: This option will set a system-wide proxy in Windows settings so all of your app's connections go through the Tor network. (Choosing this option means you don't care about leaks of your ISP when you visit websites/services.)
- **VPN MODE**: This option will apply a system-wide tunnel to capture all traffic. (This option will most likely prevent leaks but it's not guaranteed, and to make it more fool-proof changing each setting will require a re-connect.) 
- **CLEAR PROXY**: This option will clear the system proxy and no VPN tunnel will be enabled. Instead, a local proxy port (or LAN if enabled) will be open. If you use the address and port given in the "MIXED PORT" box you'll be able to manually connect specific apps to the multiplexer.

__SPLIT TUNNELING:__  
- With the "**EXCLUSIVE**" option, you can bypass the proxy for specific websites, IP addresses, or applications. You can also add items to the blacklist to prevent them from connecting to the internet entirely. 
- With the "**INCLUSIVE**" mode, you can pick specific IPs, domains, and apps to force them through Tor. *(Domain or IP bypass won't work in VPN MODE, and Application Bypass won't work in Proxy Mode.)*
- **DIRECT UDP**: This option bypasses Tor and routes all UDP traffic directly to the internet adapter (reducing anonymity, but helps with video games, Discord voice, or other UDP-dependent platforms).

__SETTINGS:__  
__START-UP:__  
- **AUTO-CONNECT**: Will immediately attempt to connect when the app is launched.
- **LAUNCH ON START-UP**: Launch the app automatically when Windows boots.
- **START MINIMIZED**: Start the app in a minimized state (depends on the MINIMIZE TO TRAY option).
- **MINIMIZE TO TRAY**: Allow the app to minimize to the system tray instead of the taskbar.

__CONNECTION:__  
- **CUSTOM XRAY EXIT-NODE**: Configure the last hop of the Tor network to be a custom Xray config. Path: `you → Tor → your Xray server → internet`. You can paste outbound JSON or import a share link (VLESS, VMess, Trojan, SS). Only ports 80 and 443 work over Tor; REALITY, KCP, and QUIC are blocked.
- **OUTBOUND PROXY**: Route your Tor traffic through an upstream HTTPS or SOCKS5 Proxy.
- **BIND ADAPTER**: Forces all Tor traffic to exclusively exit through the selected network adapter.
- **DNS SETTINGS**: 
  - **DoH**: Resolve DNS through encrypted DNS-over-HTTPS to reduce leaks and censorship (applies in Proxy and VPN modes).
  - **System DNS**: Changes the Windows DNS of your main network adapter when you connect so Tor bootstrap benefits from it.
- **AD AND TRACKER BLOCKER**: Blocks ads, trackers, and telemetry loops from known domains before they leave your PC.
- **ALLOW LAN CONNECTIONS**: Allow other devices on your local network to connect to Tor using the app (provides a LAN IP and Port). Now supports **AUTHENTICATION** so devices must supply a username and password to use this proxy.

__SYSTEM:__
- **DEBUG MODE**: Launches each backend engine's window in the command line for debugging and live log viewing.
- **LANGUAGE**: Switch between ENGLISH and PERSIAN.
- **DESKTOP / START MENU SHORTCUT**: Easily create application shortcuts.


# Credits and Donations  
Creator: [@itsTiTANVPN](https://t.me/itsTitanVPN)  

__Credits:__  
HAProxy: https://github.com/xjoker/HAProxyForWindows  
xray: https://github.com/xtls/xray-core  
Tor: https://www.torproject.org/  
Sing_Box: https://github.com/SagerNet/sing-box  
Avalonia: https://github.com/avaloniaui

__Donations:__ 
- If you want to support the project or me you can do so by sending your desired amount to one of these wallet addresses:

USDT (BEP20)  
`0xFc1d71C22DC2604f6C13Ca540ed842535cbE6d75`

USDT (TRC20)  
`TNMaNGDMG7BzbjkXeiguFWzDHZ4hCUU9R8`

BITCOIN  
`bc1quzdzuhrfse520r0wkqgkvsl7nv354r8sj5u9f9`
