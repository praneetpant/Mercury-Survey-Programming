# Mercury Survey Programming

A VB.NET Windows Forms utility that validates a Dimensions server login, downloads a GitHub repository archive, extracts it, and copies the repository contents to a configured save location using a provided new folder name.

## Build

```powershell
MSBuild ".\Mercury Survey Programming.sln" /restore /p:Configuration=Debug
```

The project targets .NET Framework 4.8 so it can be built with current Visual Studio tooling.

## Run the App

```powershell
Open `Mercury Survey Programming.sln` in Visual Studio and start the `Mercury Survey Programming` project.
```

## Startup Flow

On launch, the app checks whether required options are available:

- Dimensions URL
- Output root folder named `Mercury Survey Programming`

If either value is missing, the Setup form opens first. After setup is saved, the Login form opens and the app continues to the main downloader only when the Dimensions login is authorized.

## Setup Form

The Setup form stores settings under the current Windows user profile:

- Dimensions URLs
- Last selected Dimensions URL
- Output root folder

The Login form uses the saved Dimensions URL list as a dropdown.

The Login form can optionally save the username and password after a successful login. The password is encrypted with Windows user-level encryption before it is written to the settings file.

## Advanced Login Fields

The app still has defaults for advanced login validation:

- Authentication mode: `form` or `basic`
- Form username field name
- Form password field name
- Optional success marker text
- Optional failure marker text

Settings are stored under the current Windows user profile:

```text
%LOCALAPPDATA%\Mercury Survey Programming\settings.json
```

New Project repository mappings are stored beside the application EXE so they can be edited without rebuilding the app:

```text
<application folder>\repositories.json
```

The app includes this file automatically with:

```json
{
  "dimensions5.mercuryanalytics.com": {
    "classic": {
      "repositoryUrl": "https://github.com/praneetpant/SHELL5.git",
      "branch": "main",
      "githubToken": "",
      "githubTokenEncrypted": "",
      "oldMddName": "SHELL5"
    },
    "modern": {
      "repositoryUrl": "https://github.com/praneetpant/Templates2026.git",
      "branch": "main",
      "githubToken": "",
      "githubTokenEncrypted": "",
      "oldMddName": "SHELL2026"
    }
  }
}
```

For New Project, the current Dimensions server and selected template must match one of the keys in `repositories.json`. If it does not, the app stops and asks you to add a matching repository entry. If an older AppData repository mapping file exists and the EXE-side file is missing, the app tries to copy the older file beside the EXE.

Each template line under a Dimensions server becomes a radio option on the main form. For example, adding `"premium": { "repositoryUrl": "https://github.com/owner/repo.git", "branch": "main", "githubToken": "", "githubTokenEncrypted": "", "oldMddName": "OLDPROJECT" }` creates a `Premium Template` option automatically the next time the form loads. The app also still accepts the older shorthand format, such as `"premium": "https://github.com/owner/repo.git"`.

For private GitHub repositories on machines without Git or Visual Studio, use `GitHub Tokens...` in Setup/Options and paste a GitHub personal access token for each template. The token needs read access to the private repository. The app writes an encrypted value into that template's `githubTokenEncrypted` and keeps plain `githubToken` blank. The encrypted value is protected for the current Windows user, so copying the JSON to another machine does not provide a reusable token. Plain `githubToken` remains available only as a paste-once fallback if you edit the file directly.

You can store more than one Dimensions URL. Type a URL in Setup and click `Add`; saved URLs appear in the Dimensions URL dropdown. The login screen also shows a server dropdown so you can choose which saved Dimensions server to authorize against before the app opens the downloader.

The output location selected in Setup/Options must be the Dropbox root folder named `Mercury Survey Programming`, for example `D:\Mercury Analytics Dropbox\Praneet Pant\Mercury Survey Programming`. When a project is created or copied, the app automatically chooses the final save folder from the selected Dimensions URL and current year. For example, `dimensions5.mercuryanalytics.com` saves under `<output root>\dimensions5\2026`. On January 1, the year folder changes automatically because it uses the current system year.

By default, the app sends a form POST with fields named `username` and `password`.

Optional success/failure marker text is only needed when a server returns HTTP 200 for both good and bad credentials. In plain English: it is text the app can look for in the server response to double-check whether login worked. For example, a successful page might contain `Logout`, while a failed page might contain `Invalid`. If your Dimensions server returns proper HTTP errors for bad login, leave these blank.

## Main Form

After authorization, enter:

- Project type: `New Project` or `Copy Project`
- Project name
- Template: `Old Classic Template` or `New Modern Template` for new projects
- Existing project folder when copying a project

If the project name does not already start with `MA`, the app automatically prefixes `MA` before creating/copying. The root folder still uses only the extracted project number.

The app extracts the first 4 or 5 digit project number from the project name and uses that number to decide the folder structure inside the automatically selected `<output root>\dimensionsN\YYYY` folder.

Examples:

- If project name is `9999A Test Project` and `<save location>\9999` does not exist, the repo is copied to `<save location>\9999`.
- If `<save location>\9999` already exists, the app copies the existing contents into `<save location>\9999\9999` if that archive folder does not already exist, then copies the new repo to `<save location>\9999\9999A`.
- If the final target already exists, the app moves that existing folder to a timestamped `.oldDDMMYYHHMMSS` backup before placing the new project, so it does not overwrite project contents.

For `Copy Project`, select an existing local project folder. The app first copies that folder into a temporary Windows folder outside the save location, replaces matching old project-name text inside editable files, including `.mdd` and `.mqd` files, with the new uppercase alphanumeric project name, renames matching files/folders, renames any folder named `PROJECTFILES` to the uppercase alphanumeric project name, and then places the finished project into the configured save location.

After creating a new project from GitHub, the app also prepares the repository in a temporary Windows folder first. Editable text files, including `.mdd` and `.mqd` files, are scanned there and text matching the selected template's `oldMddName` is replaced with the new project name converted to uppercase letters/numbers only. Files and folders containing that old MDD name are also renamed, any folder named `PROJECTFILES` is renamed to the uppercase alphanumeric project name, and repository-only files `.gitattributes` and `superusers.json` are removed before the finished folder is copied into the save location.

If `oldMddName` is blank, the app uses the source repository name as the text to replace. If `branch` is blank, the app uses `main`. For private GitHub repositories, the app first tries the GitHub archive download using `githubToken` when provided, and then falls back to `git clone` using local Git credentials if Git is available. If the final project target already exists, the app moves the existing folder to a timestamped backup such as `<project path>.oldDDMMYYHHMMSS` before placing the new project.

## Super User Upload

The main window includes a `Super User` tab for uploading local template changes back to GitHub. Select the template, select the local folder, and click `Upload`. The commit message is generated automatically from the authorized Dimensions login and upload date/time. Upload progress shows the current file operation and a determinate progress bar once the file list is known.

The upload uses the selected template's repository URL, branch, and encrypted GitHub token. Before uploading, the app checks the selected local folder against the GitHub repository structure and stops if required repository files or folders are missing. Repository housekeeping files `.gitattributes`, `superusers.json`, and `.bak` backup files are ignored for this structure check. It creates or updates files in GitHub and skips unchanged files when the GitHub API returns comparable file content. Skipped unchanged files are written to the app log, and the upload-complete message offers to open the log when skips occur. It does not delete files from the repository.

The token for upload must have write access to repository contents. For fine-grained GitHub personal access tokens, grant `Contents` permission as `Read and write` for the specific repository.

The `Super User` tab is visible only when the authorized Dimensions login is in the superuser list. The default superuser is `praneetp` and cannot be removed. Click `Manage Super Users` to open the add/remove dialog. Any add/remove changes sync to `superusers.json` in the selected template repository when the dialog closes. On startup, the app tries to pull that file from GitHub before deciding whether to show the tab.
