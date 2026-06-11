# CrimsonOnion
A GUI client that runs Multiple Tor instances and Load-Balances them with HAProxy.  
This project is a re-written version of [TorMultiplexer](https://github.com/richTiTAN/Tor-Multiplexer/) in C# and .NET.  
<img width="1920" height="1080" alt="Untitled-1" src="https://github.com/user-attachments/assets/6375bada-3c29-49de-8841-37dd7a623f5f" />  

# How to use and Troubleshooting
- The interface of the app is pretty simple and tooltips have been included to help with understanding each option. But a guide is provided below nonetheless.
__Bridge Type:__  
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
