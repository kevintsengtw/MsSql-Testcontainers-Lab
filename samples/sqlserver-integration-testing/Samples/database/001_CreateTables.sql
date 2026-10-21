-- 範例資料庫結構：Shippers 與 Orders（一對多）
CREATE TABLE dbo.Shippers
(
    ShipperID   int IDENTITY (1, 1) NOT NULL,
    CompanyName nvarchar(40)        NOT NULL,
    Phone       nvarchar(24)        NULL,
    CONSTRAINT PK_Shippers PRIMARY KEY CLUSTERED (ShipperID),
    CONSTRAINT UQ_Shippers_CompanyName UNIQUE (CompanyName)
);

CREATE TABLE dbo.Orders
(
    OrderID      int IDENTITY (1, 1) NOT NULL,
    ShipperID    int                 NOT NULL,
    CustomerName nvarchar(60)        NOT NULL,
    Freight      decimal(10, 2)      NOT NULL CONSTRAINT DF_Orders_Freight DEFAULT (0),
    OrderDate    datetime2(0)        NOT NULL CONSTRAINT DF_Orders_OrderDate DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_Orders PRIMARY KEY CLUSTERED (OrderID),
    CONSTRAINT FK_Orders_Shippers FOREIGN KEY (ShipperID) REFERENCES dbo.Shippers (ShipperID)
);

CREATE INDEX IX_Orders_ShipperID ON dbo.Orders (ShipperID);
