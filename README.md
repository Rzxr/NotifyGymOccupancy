# NotifyGymOccupancy

Polls The Gym Group's app API for how busy your gym is.

## Setup

NotifyGymOccupancy reads your Gym Group login from two user environment variables. Set them once in PowerShell, replacing the placeholder values:

```powershell
[Environment]::SetEnvironmentVariable("GymGroupUserName", "you@example.com", "User")
[Environment]::SetEnvironmentVariable("GymGroupPassword", "12345678", "User")
```

Then fully restart your terminal or IDE

To use the values in the current terminal without restarting:

```powershell
$env:GymGroupUserName = [Environment]::GetEnvironmentVariable("GymGroupUserName", "User")
$env:GymGroupPassword = [Environment]::GetEnvironmentVariable("GymGroupPassword", "User")
```

To remove them:

```powershell
[Environment]::SetEnvironmentVariable("GymGroupUserName", $null, "User")
[Environment]::SetEnvironmentVariable("GymGroupPassword", $null, "User")
```

### Discord

Each reading is posted to a Discord channel through a webhook. In Discord, open the channel's **Edit Channel → Integrations → Webhooks**, create a webhook, copy its URL, then set it the same way:

```powershell
[Environment]::SetEnvironmentVariable("DiscordWebhookUrl", "https://discord.com/api/webhooks/...", "User")
```

To watch a gym other than your home gym, set `GymGroup:GymLocationId` in `src/appsettings.json`.

## Schedule

It only polls inside the windows under `Schedule:Windows` in `src/appsettings.json` (local time, `End` exclusive), and polls around the clock if the list is empty. By default:

- Tuesday–Friday, 16:00–21:00
- Saturday–Sunday, 14:00–18:00
- UK bank holidays, 14:00–18:00 (`Schedule:BankHolidayWindows`, which replaces the normal windows for that day, including Mondays)

Bank holidays come from [GOV.UK](https://www.gov.uk/bank-holidays.json), saved to `%LOCALAPPDATA%\NotifyGymOccupancy\bank-holidays.json` and refreshed on the first Tuesday of each month (or the first run after it, if the PC was off). Set `Schedule:BankHolidayDivision` to `scotland` or `northern-ireland` if you're not in England or Wales.

## History

Every reading is appended to `%LOCALAPPDATA%\NotifyGymOccupancy\history.csv`, which opens in Excel. Once there are a few readings from earlier weeks, each Discord message also says whether it's quieter or busier than usual, comparing with readings within 30 minutes of the same time on the same weekday (or on bank holidays) over the last 90 days.

## Run

From the repository root:

```bash
dotnet run --project src
```

## Run automatically when you log in

From the repository root, publish a separate copy into `release` so rebuilding doesn't touch the running one:

```powershell
dotnet publish src -c Release -o release
```

Then register a Task Scheduler task that starts it hidden at logon and never times out:

```powershell
$dir = (Resolve-Path release).Path
$action = New-ScheduledTaskAction -Execute "conhost.exe" -Argument "--headless `"$dir\NotifyGymOccupancy.exe`"" -WorkingDirectory $dir
$trigger = New-ScheduledTaskTrigger -AtLogOn -User "$env:USERDOMAIN\$env:USERNAME"
$settings = New-ScheduledTaskSettingsSet -ExecutionTimeLimit ([TimeSpan]::Zero) -RestartCount 3 -RestartInterval (New-TimeSpan -Minutes 5) -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries
Register-ScheduledTask -TaskName "NotifyGymOccupancy" -Action $action -Trigger $trigger -Settings $settings
```

Start it now without logging out with `Start-ScheduledTask NotifyGymOccupancy`, stop it with `Stop-ScheduledTask NotifyGymOccupancy`, and remove it with `Unregister-ScheduledTask NotifyGymOccupancy`. The task keeps running the old version until you publish again. To release changes to the code or `src/appsettings.json`, run:

```powershell
.\release.ps1
```

It stops the task, publishes into `release`, and starts the task again. If the publish fails, the task is left stopped so you can fix it and rerun.
