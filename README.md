Configuration
```
<?xml version="1.0" encoding="utf-8"?>
<Config xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <DiscordBotToken>Bot Token</DiscordBotToken>
  <ServerName>Name</ServerName>
  <DiscordWebHook>https://discord.com/api/webhooks/...</DiscordWebHook>
</Config>
```
Translation
```
<?xml version="1.0" encoding="utf-8"?>
<Translations xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <Translation Id="message" Value="**Постройка уничтожена!**&#xA;**Ближайшая локация:** {7}&#xA;**Сервер:** {8}&#xA;" />
  <Translation Id="messageToServer" Value="**Постройка уничтожена!**&#xA;**Владелец:** [{0}]({1})&#xA;**Уничтожил:** [{2}]({3})&#xA;**Оружие:** {4}&#xA;**Постройка:** {5} (ID: {6})&#xA;**Ближайшая локация:** {7}&#xA;**Сервер:** {8}&#xA;" />
</Translations>
```
In the translator, to transfer parameters (message / messageToServer), use:
{0} ownerName
{1} ownerProfile
{2} destroyerName
{3} destroyerProfile
{4} weaponName
{5} buildName
{6} buildId
{7} locationInfo
{8} ServerName
