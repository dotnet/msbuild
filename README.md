ASP.NET Web Sdk targets
======================

ASP.NET Web Sdk targets contains the tasks, targets and packages required to build and publish Web Applications.

Publish Commandline Usage:

1. Folder publish without a profile (Default Publish):
    - msbuild WebApplication.csproj /p:DeployOnBuild=true /p:PublishUrl="C:\deployedApp\newapp"
    or
    - dotnet msbuild WebApplication.csproj /p:DeployOnBuild=true /p:PublishUrl="C:\deployedApp\newapp"

2. Folder Publish with a profile:
    - msbuild WebApplication.csproj /p:DeployOnBuild=true /p:PublishProfile=<FolderProfileName>
    or
    - dotnet msbuild WebApplication.csproj /p:DeployOnBuild=true /p:PublishUrl="C:\deployedApp\newapp"

3. MSDeploy Publish with a profile:
    - msbuild WebApplication.csproj /p:DeployOnBuild=true /p:PublishProfile=<MsDeployProfileName> /p:Password=<DeploymentPassword>
    (Not available on DotNet Core yet)

4. MsDeploy Package Publish with a profile:
    - msbuild WebApplication.csproj /p:DeployOnBuild=true /p:PublishProfile=<MsDeployProfileName>
    (Not available on DotNet Core yet)

Sample EF Migrations section:
<ItemGroup>
    <DestinationConnectionStrings Include="ShoppingCartConnection">
      <Value>Data Source=tcp:dbserver.database.windows.net,1433;Initial Catalog=shoppingcartdbdb_db;User Id=appUser@dbserver;Password=password</Value>
    </DestinationConnectionStrings>
</ItemGroup>
<ItemGroup>
    <EFMigrations Include="ShoppingCartContext">
      <Value>Data Source=tcp:dbserver.database.windows.net,1433;Initial Catalog=shoppingcartdbdb_db;User Id=efMigrationUser@dbserver;Password=password</Value>
    </EFMigrations>
</ItemGroup>

 
Sample Folder Profile:

<?xml version="1.0" encoding="utf-8"?>
<Project ToolsVersion="4.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
    <PropertyGroup>
    <WebPublishMethod>FileSystem</WebPublishMethod>
    <PublishProvider>FileSystem</PublishProvider>
    <LastUsedBuildConfiguration>Release</LastUsedBuildConfiguration>
    <LastUsedPlatform>Any CPU</LastUsedPlatform>
    <PublishFramework>netcoreapp1.0</PublishFramework>
    <publishUrl>bin\Release\PublishOutput</publishUrl>
    <DeleteExistingFiles>False</DeleteExistingFiles>
  </PropertyGroup>  
</Project>

Sample MsDeploy Publish Profile:

<?xml version="1.0" encoding="utf-8"?>
<Project ToolsVersion="4.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <PropertyGroup>
    <WebPublishMethod>MSDeploy</WebPublishMethod>
    <PublishProvider>AzureWebSite</PublishProvider>
    <LastUsedBuildConfiguration>Release</LastUsedBuildConfiguration>
    <LastUsedPlatform>Any CPU</LastUsedPlatform>
    <SiteUrlToLaunchAfterPublish>http://webappwithdb.azurewebsites.net</SiteUrlToLaunchAfterPublish>
    <LaunchSiteAfterPublish>True</LaunchSiteAfterPublish>
    <PublishFramework>netcoreapp1.0</PublishFramework>
    <MSDeployServiceURL>webappwithdb.scm.azurewebsites.net:443</MSDeployServiceURL>
    <DeployIisAppPath>webappwithdb</DeployIisAppPath>
    <SkipExtraFilesOnServer>True</SkipExtraFilesOnServer>
    <MSDeployPublishMethod>WMSVC</MSDeployPublishMethod>
    <EnableMSDeployBackup>True</EnableMSDeployBackup>
    <UserName>$vramakwebappwithdb</UserName>
	<Password>DeployPassword</Password>
  </PropertyGroup>
</Project>

Sample MsDeploy Package Publish Profile

<?xml version="1.0" encoding="utf-8"?>
<Project ToolsVersion="4.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <PropertyGroup>
    <WebPublishMethod>Package</WebPublishMethod>
    <LastUsedBuildConfiguration>Release</LastUsedBuildConfiguration>
    <LastUsedPlatform>Any CPU</LastUsedPlatform>
    <PublishFramework>netcoreapp1.0</PublishFramework>
    <DesktopBuildPackageLocation>c:\DeployedApp\WebDeployPackage.zip</DesktopBuildPackageLocation>
    <DeployIisAppPath>Default Web Site/WebAppWithDB</DeployIisAppPath>
  </PropertyGroup>
</Project>


Sample MsDeploy Profile With Destination Connection String & EF Migrations

<?xml version="1.0" encoding="utf-8"?>
<Project ToolsVersion="4.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <PropertyGroup>
    <WebPublishMethod>MSDeploy</WebPublishMethod>
    <PublishProvider>AzureWebSite</PublishProvider>
    <LastUsedBuildConfiguration>Release</LastUsedBuildConfiguration>
    <LastUsedPlatform>Any CPU</LastUsedPlatform>
    <SiteUrlToLaunchAfterPublish>http://webappwithdb.azurewebsites.net</SiteUrlToLaunchAfterPublish>
    <LaunchSiteAfterPublish>True</LaunchSiteAfterPublish>
    <PublishFramework>netcoreapp1.0</PublishFramework>
    <MSDeployServiceURL>webappwithdb.scm.azurewebsites.net:443</MSDeployServiceURL>
    <DeployIisAppPath>webappwithdb</DeployIisAppPath>
    <SkipExtraFilesOnServer>True</SkipExtraFilesOnServer>
    <MSDeployPublishMethod>WMSVC</MSDeployPublishMethod>
    <EnableMSDeployBackup>True</EnableMSDeployBackup>
    <UserName>$vramakwebappwithdb</UserName>
	<Password>DeployPassword</Password>
  </PropertyGroup>
  <ItemGroup>
    <DestinationConnectionStrings Include="ShoppingCartConnection">
      <Value>Data Source=tcp:dbserver.database.windows.net,1433;Initial Catalog=shoppingcartdbdb_db;User Id=appUser@dbserver;Password=password</Value>
    </DestinationConnectionStrings>
  </ItemGroup>
  <ItemGroup>
    <EFMigrations Include="ShoppingCartContext">
      <Value>Data Source=tcp:dbserver.database.windows.net,1433;Initial Catalog=shoppingcartdbdb_db;User Id=efMigrationUser@dbserver;Password=password</Value>
    </EFMigrations>
  </ItemGroup>
</Project>


