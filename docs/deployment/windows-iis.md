# Windows/IIS deployment

This guide describes deployment to a small Azure Windows Server virtual machine for Henri.

## Prepare the Azure VM

1. Create a small Azure Windows Server VM with a fixed hostname or DNS name for the application.
2. Restrict inbound access with the Azure network security group. Allow HTTP/HTTPS as required and limit RDP access to the administration network.
3. Connect to the VM with an administrator account.
4. Install the IIS Web Server role using Server Manager or PowerShell.
5. Install the .NET Hosting Bundle version matching the application target framework. Restart the VM if the installer requests it.

## Publish and copy the application

Run the following command from the repository root on the deployment machine:

```powershell
dotnet publish src/KissakiSignup.Web/KissakiSignup.Web.csproj -c Release -o artifacts/publish
```

Create the IIS application directory and copy the complete contents of `artifacts/publish` to:

```text
C:\inetpub\KissakiSignup
```

Keep `App_Data` on the server and do not place the SQLite database in a publicly served directory.

## Configure IIS

1. Create an IIS application pool for Kissaki Signup.
2. Set the application pool's .NET CLR version to `No Managed Code`.
3. Set the application pool identity to the identity used by the site.
4. Create an IIS site pointing to `C:\inetpub\KissakiSignup` and assign the application pool.
5. Configure the site binding and HTTPS certificate as required for the production hostname.
6. Grant the application pool identity write permission to:

   ```text
   C:\inetpub\KissakiSignup\App_Data
   ```

   The application needs this permission to create and update the SQLite database.

## Set tournament configuration

Set these environment variables for the IIS worker process or application configuration:

```text
Tournament__AdminPassword=<strong-production-password>
Tournament__DatabasePath=App_Data/kissaki-registration.sqlite
Tournament__TournamentName=Kissaki Cup 2026
Tournament__RegistrationOpen=true
Tournament__RegistrationDeadline=2026-10-11
```

Use the double-underscore names exactly as shown. Do not store the production admin password in the repository. Restart the IIS site or recycle its application pool after changing configuration.

## Backups and KTM exports

Take a dated backup before every KTM export. Do not copy only the main `.sqlite` file while the site is running: SQLite may have uncheckpointed data in `-wal` and `-shm` files.

For a simple offline backup, stop the IIS site or its application pool first, copy the database to a restricted directory outside the application, then start it again. A clean stop allows SQLite to close and checkpoint the database before the copy.

```powershell
Stop-WebAppPool -Name '<KissakiSignup application pool>'
Copy-Item 'C:\inetpub\KissakiSignup\App_Data\kissaki-registration.sqlite' 'D:\KissakiBackups\kissaki-registration-YYYY-MM-DD.sqlite'
Start-WebAppPool -Name '<KissakiSignup application pool>'
```

Alternatively, while the site is running, use SQLite's online backup command rather than a file copy. It creates a consistent copy even when WAL mode is active:

```powershell
sqlite3 'C:\inetpub\KissakiSignup\App_Data\kissaki-registration.sqlite' ".backup 'D:\KissakiBackups\kissaki-registration-YYYY-MM-DD.sqlite'"
```

After either method, check the backup and periodically test a restore on a copy while the site is stopped:

```powershell
sqlite3 'D:\KissakiBackups\kissaki-registration-YYYY-MM-DD.sqlite' 'PRAGMA integrity_check;'
```

Expect `ok`. Store backups outside the application directory with restricted access. Take the backup before downloading the export files from:

```text
/admin/export/clubs.csv
/admin/export/participants.csv
/admin/export/teams.csv
```

## Close registration

When registration must close, set:

```text
Tournament__RegistrationOpen=false
```

Then restart the IIS site, or recycle its application pool, so the new setting is loaded. Confirm that the public registration page reports that registration is closed before distributing the KTM exports.
