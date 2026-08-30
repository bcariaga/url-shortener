# Bruno collection

Open this directory as a collection in Bruno and select the `local`
environment. The environment targets the API profile from
`Api/Properties/launchSettings.json` at `http://localhost:5080`.

Before sending authenticated requests, set the secret `token` environment
variable in Bruno to the same value configured under
`ManagementAuth:Tokens:0:Token` in .NET user secrets. The token is intentionally
not stored in this repository.

Run the requests in sequence. `Create short URL` stores the returned six-character
code as a runtime variable, so update, redirect, and delete use the resource that
was just created. When running an individual request, set `shortCode` manually in
the local environment first.
