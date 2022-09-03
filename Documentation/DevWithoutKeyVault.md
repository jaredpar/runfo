# Development without Azure KeyVault

Several of the runfo apps store secrets in Azure Key Vault and the apps will load configuration from there by default.
However if your user account doesn't have access to the Azure Key Vault or for whatever other reason you don't want to use
Key Vault there is an alternative approach. This will require you to provide various tokens and connection strings
manually. At present only the DevOps.Status and scratch project are coded to support this but it wouldn't be hard to add it
to others. 

## 1. GitHub tokens

Runfo uses OAuth to authenticate the current user to GitHub which requires creating an app registration.
Follow the [instructions from GitHub](https://docs.github.com/en/developers/apps/building-oauth-apps/creating-an-oauth-app)
to create one. The app name, app description and web page don't matter other than maybe needing to be unique. They will just
be displayed back to you later during login. The callback URL should point to the /signin-github page where you will host
the runfo web app. For example:
 - Name: Runfo_Noah_Local_Dev
 - URL: https://github.com/noahfalk/runfo
 - Description: blank
 - Callback URL: https://localhost:5001/signin-github

 After registering you will get a client id and client secret, save these for later.

 By default Runfo also uses a GitHub App (OAuth apps and GH apps are not the same thing) which it uses for managing
 issues. If you don't have Azure Key Vault access you probably don't have access to the AppId and AppSecret tokens for the
 GitHub app either. There is a GitHubImpersonateUser configuration option we'll set later which avoids using the GH app and instead
 will impersonate your logged in GH identity when making changes to issues.

## 2. Runfo Database

Runfo uses a database to store tracking issues and caching data from AzDo and Helix. You will either need to obtain a connection
string for the live test/prod database or set up your own. To set one up I recommend installing the Developer edition of SQL 
and include the Full Text Indexing optional feature. You can't use SQL Express or the LocalDB that comes with VS because these
editions do not support Full Text Search.

## 3. AzDo Personal Access Token

Get a PAT for your user account at https://dev.azure.com/dnceng. See the
[AzDo docs](https://docs.microsoft.com/en-us/azure/devops/organizations/accounts/use-personal-access-tokens-to-authenticate?view=azure-devops&tabs=Windows)
for more details. Select Read access for the Work Items, Code, Build, Release, and Test Management scopes. (I guessed at these
permissions and they appear to work but it might be more than is needed).

Save the PAT for later.

## 4. Create User Secrets

DotNet supports saving per-user secrets in a file called secrets.json that is separate from your project source so that
you don't accidentally check it in. You can create the file using Visual Studio's 'Manage User Secrets' or via command-line using
dotnet user-secrets. See [the docs](https://docs.microsoft.com/en-us/aspnet/core/security/app-secrets?view=aspnetcore-6.0&tabs=windows)
for more details.

Edit the secrets.json file so it looks like the one below. Use the client id and client secret from step 1, whatever connection
string is appropriate for the database you set up in step 2, and RunfoAzdoToken is the token from step 3.

```
{
  "GitHubClientId": "11122233344455566677",
  "GitHubClientSecret": "11112222333334444555666777888999aaabbbcc",
  "RunfoAzdoToken": "abcdefghijklmnopqrstuvwxyz1234567890abcdefghijklmnop",
  "RunfoConnectionString": "Server=localhost;Database=TriageContext;Trusted_Connection=True;",
  "GitHubImpersonateUser": "true"
}
```

GitHubImpersonateUser isn't really a secret, but it was convenient to set it here because these are per-user settings that won't
get checked in.

## 5. Set the environment variable USE_KEYVAULT=0

Program.cs for the DevOps.Status and scratch projects will check for this env var and avoid using Azure Key Vault in the configuration
setup. The config values that normally would have come from key vault will come from the secrets.json instead.

## 6. Create the Runfo TriageContext database if needed

If you installed a new SQL server instance in step 2 then we need to initialize Runfo's database. Navigate to the scratch
source folder.

If needed install the ef command line tools:

```
dotnet tool install -g dotnet-ef
```

Once the tools are installed run this command to initialize the database schema:

```
dotnet ef database update
```

Then run the scratch project with argument "populateDb". This will take a long time as scratch queries AzDo and Helix
caching lots of data into the new runfo database. You can quit part way through if you don't care about getting
complete data.

## 7. Start developing

You should now be able to run the runfo apps and have things mostly function. Use dotnet run or launch from VS with F5.
If you use the debugger make sure USE_KEYVAULT=0 is included in your launch profile.
