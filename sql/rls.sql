create schema [Security] authorization [dbo]
go

create table [Security].[SalesTerritoryAccess]
(
    [Id] int not null identity primary key,
    [UserAccount] varchar(100) collate Latin1_General_BIN2 not null,
    [TerritoryID] int not null
)
go

delete from [Security].[SalesTerritoryAccess]
insert into [Security].[SalesTerritoryAccess]
    (UserAccount, TerritoryID) 
values 
    (suser_sname(), 1),
    (suser_sname(), 4)
go

select suser_sname()
select * from [Security].[SalesTerritoryAccess]
go

create function [Security].[CheckSalesTerritoryAccess](@TerritoryID as int)
returns table
with schemabinding
as
return
    select 1 as authorized from [Security].[SalesTerritoryAccess]
    where [UserAccount] = suser_sname() and [TerritoryID] = @TerritoryID
go

create security policy [Security].[SalesTerritoryPolicy]
add filter predicate [Security].[CheckSalesTerritoryAccess]([TerritoryID]) on [Sales].[SalesTerritory],
add filter predicate [Security].[CheckSalesTerritoryAccess]([TerritoryID]) on [Sales].[SalesOrderHeader],
add filter predicate [Security].[CheckSalesTerritoryAccess]([TerritoryID]) on [Sales].[Customer],
add filter predicate [Security].[CheckSalesTerritoryAccess]([TerritoryID]) on [Sales].[SalesPerson]
with (state = off)
go

alter security policy [Security].[SalesTerritoryPolicy]
with (state = on)
go

select * from sys.security_policies
go

alter security policy [Security].[SalesTerritoryPolicy]
with (state = off)
go