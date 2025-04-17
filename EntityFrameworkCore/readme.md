### EF Tips
* If db schema changes, you can update data model project as follows (.NET 6 EF Database First)
    * Install these nuget packages (if not installed yet): Microsoft.EntityFrameworkCore.Design, Microsoft.EntityFrameworkCore.SqlServer, Microsoft.EntityFrameworkCore.Tools
    * Run the scaffold command in *Package Manager Console*: *Scaffold-DbContext {CONNECTION_STRING} Microsoft.EntityFrameworkCore.SqlServer -OutputDir Models*
    * To re-scaffold, run: *Scaffold-DbContext {CONNECTION_STRING} Microsoft.EntityFrameworkCore.SqlServer -OutputDir Models -Context MyDBContext -f*
        * **E.g**. *Scaffold-DbContext "Data Source=localhost;Initial Catalog=cxp;Integrated Security=True;TrustServerCertificate=true;" Microsoft.EntityFrameworkCore.SqlServer -OutputDir Models -Context MyDBContext -f*
          Note: The "*TrustServerCertificate=true*" is necessary to get rid of a certificate error.
    * When re-scaffolding EF data models, make sure the name of the EF data context matches that in *appsettings.json*.
