# Working with Secrets

## Managing dotnet user-secrets

The easiest way to setup the GitHub set of secrets for this application is to import them via a json
file. For example:

```json
{
	"GitHubAppId": 42,
	"GitHubAppPrivateKey": "...",
	"GitHubClientId": "...",
	"GitHubClientSecret": "...",
}
```

Once that is defined it can be mass imported into the user secret store with the following:

```
> type secrets.json | dotnet user-secrets set
```

One complication is that JSON literals don't support line breaks. That means they need to be escaped
with literal `\n` characters. This comes up with the PEM file in `GitHubAppPrivateKey`. To fix this just
paste in the file and then run the vim command:

```
:s/\n/\\n/g
```

To set the secrets in Azure take a similar approach of doing an advanced edit and typing in the JSON
directly.
