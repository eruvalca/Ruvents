# Ruvents solution template

Personal Windows/.NET 10 template. Create files with:

```powershell
dotnet new ruvents --name MyNewApp --output D:\repos\MyNewApp
```

Use a PascalCase ASCII name, starting with a letter and containing only letters
and digits (1–40 characters). Windows device names, dotted names, spaces, and
hyphens are unsupported. The output directory may contain spaces.

Enter the generated directory and follow its README. Requires the .NET SDK,
Aspire CLI, Docker, and trusted development HTTPS certificate. Creation runs no
restore or startup commands. Local credentials and database contents are not copied.

This package is a source snapshot; updating it does not update existing applications.
The generated `.template-provenance.json` identifies its version and source commit.
