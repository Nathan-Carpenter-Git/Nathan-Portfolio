# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project overview

Nathan Carpenter's personal portfolio site: an ASP.NET Core MVC (.NET 8) web app with three pages/controllers (Home, Contact, TalkToMe) hosted on Azure App Service.

## Commands

```bash
dotnet build                    # build
dotnet run                      # run locally (see launchSettings.json for ports/profiles)
dotnet watch run                # run with hot reload
```

There are no automated tests and no CI workflows configured (`.github/workflows/` is empty) - verify changes by running the app and exercising the page in a browser.

## Architecture

Standard ASP.NET Core MVC layout - `Controllers/`, `Models/`, `Views/{ControllerName}/`, `wwwroot/`. Routing is the default convention (`{controller=Home}/{action=Index}/{id?}`), registered in `Program.cs`.

- **HomeController** - static landing page (`Views/Home/Index.cshtml`).
- **ContactController** - renders a contact form and, on POST, sends an email via `IEmailSender` (`CustomServices/EmailSender.cs`), then redirects back to itself with `?ResponseMessage=...` for display.
- **TalkToMeController** - an AI chat page. `GET /TalkToMe` renders the chat UI; `POST /TalkToMe/SendMessage` takes a JSON body (`{ message, history, systemContext }`), forwards the conversation to `IOpenRouterService`, and returns `{ reply }`. Client-side chat state (message history) lives in `wwwroot/js/talktome.js` and is resent in full on every request - the server is stateless between calls.

### Secrets via Azure Key Vault

There are no API keys or credentials in config files. `EmailSender` and `OpenRouterService` each construct their own `SecretClient` (Azure.Security.KeyVault.Secrets) against the vault URL in `appsettings.json` (`VaultURL`), authenticating with `DefaultAzureCredential`. Secrets fetched by name:
- `Send--From--Email`, `Send--Email`, `Email--Pass` (SMTP2GO credentials, used by `EmailSender`)
- `OpenRouterApiKey` (used by `OpenRouterService`, cached in-memory after first fetch)

When running locally, `DefaultAzureCredential` needs a signed-in identity (e.g. `az login` or Visual Studio/VS Code Azure account) with access to the `nathanportfoliovault` Key Vault, or these features will fail.

### TalkToMe / OpenRouter integration

`OpenRouterService` (`CustomServices/OpenRouterService.cs`) calls OpenRouter's chat completions API (`openrouter/free` model). It hardcodes a system-prompt prefix containing Nathan's resume/background so the assistant answers in-character as Nathan; the caller-supplied `systemContext` is appended after it. History is trimmed from the oldest messages first if the estimated character count would exceed the model's context window (`SafeCharLimit`).

### Frontend

No JS bundler/build step - plain jQuery/Bootstrap plus hand-written vanilla JS (`wwwroot/js/site.js`, `wwwroot/js/talktome.js`) and CSS (`wwwroot/css/site.css`, `wwwroot/css/nathan-portfolio.css`), referenced directly from `Views/Shared/_Layout.cshtml`. Static assets are served as-is from `wwwroot/`; no `npm`/`package.json` in this project.
