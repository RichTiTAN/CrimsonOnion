# CrimsonOnion
A GUI client that runs Multiple Tor instances and Load-Balances them with HAProxy.  
This project is a re-written and improved version of [TorMultiplexer](https://github.com/richTiTAN/Tor-Multiplexer/) in C# and .NET.  

<img width="3100" height="1080" alt="Untitl213ed-4" src="https://github.com/user-attachments/assets/d0f42c6e-654f-4f1c-ad1b-059b63d32b6b" />


__HOW IT WORKS:__
- This app uses 1-8 Tor connections, load-balances them with HAProxy, uses Xray-core for managing and enabling the Proxy and Sing-box for managing the VPN MODE.
- [FARSI GUIDE](https://github.com/RichTiTAN/CrimsonOnion/blob/main/readmeFA.md)
  
# How to use and Troubleshooting
- The interface of the app is pretty simple and tooltips have been included to help with understanding each option. But a guide is provided below nonetheless.

__BRIDGE TYPE:__  
- This option will show you different bridge types you can use to connect to Tor.
- On healthy networks you can use "Direct".
- If Tor is blocked in your region you can choose other bridge types, Try with this order: Obfs4, Snowflake, meek_lite and see which one works better for you and your network interface.
- Custom Bridges: By opening the Custom Bridge menu you can request webtunnel or obfs4 bridges, these are known to work very well in restricted areas. alternetively if you can't fetch bridges from inside the app you can try the alternative ways of getting a new bridge line. 

__ROUTING:__  
- Recommended routing option as set by default is "Optimized", By selecting Optimized a series of recommended settings for your Tor configuration will be applied. This will potentially connect the Tor engines to different locations which may increase the chance of getting faced with a captcha whenever you enter a website.
- If you rather have a single exit location (which may reduce the chance of facing captchas) you can choose the "Custom" option and choose your desired country for the exit-node.
- The "Expert" option is either for people who want the barebone Tor experience with no adjustments or for people who want the maximum flexibility with their Tor configurations. for the barebone experience you have to click "Expert" then click "Save" on the opened window.

__TOR ENGINES:__
- You can set the amount of Tor instances that you want to run and load-balance, higher doesn't always mean faster, 6 is the recommended amount for the use on a single device. you can go higher if you want to connect other devices to the app.

__OPTIONS:__
- SESSION:OFFLINE will show the connection time and starts after you've established a connection.
- AUTO-CONNECT will immediately attempt to connect when the app is launched.
- ADVANCED SETTINGS will open the advanced settings menu for you to adjust the extra options.

__CONNECTION:__
- PROXY MODE: This option will set a system-wide proxy in windows settings so all of your app's connections go through the Tor network. (Choosing this option means you don't care about leaks of your ISP when you visit websites/services.)
- VPN MODE: This option will apply a system wide tunnel to capture all traffic. (This option will most likely prevent leaks but it's not guaranteed and to make it more fool-proof changing each setting will require a re-connect.) 
- CLEAR PROXY: This option will clear the system proxy and no VPN tunnel will be enabled, instead a local proxy port (or LAN if you have the setting on) will be open on port 10818. If you use the address and port give in the "MIXED PORT" box you'll be able to connect to the Tor Multiplexer.

__ADVANCED SETTINGS:__  
__ROUTING:__  
- SPLIT TUNNELING: With "EXCLUSIVE" option you can bypass the proxy for specific websites, ip addresses or applications and you can add websites or ip addresses to the blacklist to prevent them from connecting to the internet entirely. With "INCLUSIVE" mode you can pick the desired IPs, domains and apps to go through Tor. (Domain or IP bypass won't work in VPN MODE and Application Bypass won't work in Proxy Mode.)
- CUSTOM V2RAY EXIT-NODE: With this option you can configure the last hop of the Tor network to be a custom Xray config. This will help Tor support UDP (with UDP over TCP) and it will give you a static IP address which you might need for sensitive websites. (The port of the config needs to be 443 or 80 as Tor might block other ports.)
- OUTBOUND PROXY: With this option you can route your Tor traffic through an upstream HTTPS/SOCKS5 Proxy.
- AD AND TRACKER BLOCKER: This option will block ads, trackers and telemetry loops.
- ALLOW LAN CONNECTIONS: This option will allow other devices on your local network to connect to Tor using the app. After enabling the app will provide you with a LAN IP and Port on connection, which you can use in any proxy management app in your phone to connect to the app. (HTTP, HTTPS, SOCKS3,4,5 are supported.)

__SYSTEM:__
- LAUNCH ON START-UP: Enabling this option will launch the app when windows boots.
- START MINIMIZED: Enabling This option will start the app in a minimized state with respect to MINIMIZE TO TRAY option.
- MINIMIZE TO TRAY: Enabling this option will allow the app to minimize to the system tray instead of the task bar.
- DEBUG MODE: Enabling this option will launch each app's window in cmd for debugging purposes.
- LIVE LOGS: This option will open up a logs panel which you can see the Tor boostrap status in and monitor the Xray logs.
- CREATE DESKTOP SHORTCUT: Creates a desktop shortcut for the app?

__UNIFIED PANEL:__
- MIXED PORT: This panel will show you the Local and LAN ip address and ports that you can use to connect to the Multiplexer if you'd like to.
- STATS: This panel will provide you with the current network speed of the app, total amount of data used, location of the connection and the ping to the servers. If you click the panel another ping/location test will be taken.

# Credits and Donations  
Creator: [@itsTiTANVPN](https://t.me/itsTitanVPN)  

__Credits:__  
HAProxy: https://github.com/xjoker/HAProxyForWindows  
xray: https://github.com/xtls/xray-core  
Tor: https://www.torproject.org/  
Sing_Box: https://github.com/SagerNet/sing-box

__Donations:__ 
- If you want to support the project or me you can do so by sending your desired amount to one of these wallet addresses:

USDT (BEP20)  
0xFc1d71C22DC2604f6C13Ca540ed842535cbE6d75

USDT (TRC20)  
TNMaNGDMG7BzbjkXeiguFWzDHZ4hCUU9R8

BITCOIN  
bc1quzdzuhrfse520r0wkqgkvsl7nv354r8sj5u9f9
