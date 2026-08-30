using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AffiliateSuperstore.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialPersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Products",
                columns: table => new
                {
                    AliExpressProductId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ProductDetailUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    MainImageUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    FirstLevelCategoryId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    FirstLevelCategoryName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    SecondLevelCategoryId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    SecondLevelCategoryName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    SellerId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    SellerName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SellerUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    IsEligible = table.Column<bool>(type: "bit", nullable: false),
                    IneligibilityReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    FirstSeenUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastSeenUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastRefreshedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.AliExpressProductId);
                });

            migrationBuilder.CreateTable(
                name: "Shops",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    PathPrefix = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CanonicalHostname = table.Column<string>(type: "nvarchar(253)", maxLength: 253, nullable: true),
                    TrackingId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SubAffiliateCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DefaultSearchQuery = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    SeoTitle = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SeoDescription = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    PrimaryColour = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AccentColour = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Shops", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProductSnapshots",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    FetchedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    SalePrice = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    OriginalPrice = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    CommissionRate = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: true),
                    HotProductCommissionRate = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: true),
                    DiscountText = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    EvaluationRate = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: true),
                    RecentSalesVolume = table.Column<long>(type: "bigint", nullable: true),
                    TaxRate = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: true),
                    IsAvailable = table.Column<bool>(type: "bit", nullable: true),
                    DeliveryDays = table.Column<int>(type: "int", nullable: true),
                    RawJson = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductSnapshots_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "AliExpressProductId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AffiliateLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ShopId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    SourceUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    PromotionUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    TrackingId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PromotionLinkType = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    GeneratedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastValidatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ExpiresUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AffiliateLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AffiliateLinks_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "AliExpressProductId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AffiliateLinks_Shops_ShopId",
                        column: x => x.ShopId,
                        principalTable: "Shops",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IngestionJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ShopId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Type = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Checkpoint = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    ItemsRead = table.Column<int>(type: "int", nullable: false),
                    ItemsWritten = table.Column<int>(type: "int", nullable: false),
                    ItemsRejected = table.Column<int>(type: "int", nullable: false),
                    QueuedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    StartedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CompletedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ErrorSummary = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IngestionJobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IngestionJobs_Shops_ShopId",
                        column: x => x.ShopId,
                        principalTable: "Shops",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ShopProducts",
                columns: table => new
                {
                    ShopId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsFeatured = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    EditorialTitle = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    EditorialDescription = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    DisabledReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    FirstIncludedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastIncludedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShopProducts", x => new { x.ShopId, x.ProductId });
                    table.ForeignKey(
                        name: "FK_ShopProducts_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "AliExpressProductId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ShopProducts_Shops_ShopId",
                        column: x => x.ShopId,
                        principalTable: "Shops",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OutboundClicks",
                columns: table => new
                {
                    ClickId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ShopId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    AffiliateLinkId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TrackingId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Campaign = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Placement = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AnonymousSessionHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ClickedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ConvertedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboundClicks", x => x.ClickId);
                    table.ForeignKey(
                        name: "FK_OutboundClicks_AffiliateLinks_AffiliateLinkId",
                        column: x => x.AffiliateLinkId,
                        principalTable: "AffiliateLinks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_OutboundClicks_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "AliExpressProductId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_OutboundClicks_Shops_ShopId",
                        column: x => x.ShopId,
                        principalTable: "Shops",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AffiliateOrders",
                columns: table => new
                {
                    SubOrderId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ParentOrderId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ClickId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    TrackingId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CustomParameters = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ProductId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ProductTitle = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CommissionRate = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: true),
                    EstimatedPaidCommission = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    EstimatedFinishedCommission = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    PaidAmount = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    FinishedAmount = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    SettledCurrency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    PaidUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    FinishedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CompletedSettlementUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ShipToCountry = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    IsAffiliateProduct = table.Column<bool>(type: "bit", nullable: true),
                    IsHotProduct = table.Column<bool>(type: "bit", nullable: true),
                    FirstSeenUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastSeenUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RawJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AffiliateOrders", x => x.SubOrderId);
                    table.ForeignKey(
                        name: "FK_AffiliateOrders_OutboundClicks_ClickId",
                        column: x => x.ClickId,
                        principalTable: "OutboundClicks",
                        principalColumn: "ClickId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AffiliateLinks_ExpiresUtc",
                table: "AffiliateLinks",
                column: "ExpiresUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AffiliateLinks_ProductId",
                table: "AffiliateLinks",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_AffiliateLinks_ShopId_ProductId_Status",
                table: "AffiliateLinks",
                columns: new[] { "ShopId", "ProductId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AffiliateOrders_ClickId",
                table: "AffiliateOrders",
                column: "ClickId");

            migrationBuilder.CreateIndex(
                name: "IX_AffiliateOrders_CompletedSettlementUtc",
                table: "AffiliateOrders",
                column: "CompletedSettlementUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AffiliateOrders_Status_LastSeenUtc",
                table: "AffiliateOrders",
                columns: new[] { "Status", "LastSeenUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_IngestionJobs_ShopId_Type_StartedUtc",
                table: "IngestionJobs",
                columns: new[] { "ShopId", "Type", "StartedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_IngestionJobs_Status_QueuedUtc",
                table: "IngestionJobs",
                columns: new[] { "Status", "QueuedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_OutboundClicks_AffiliateLinkId",
                table: "OutboundClicks",
                column: "AffiliateLinkId");

            migrationBuilder.CreateIndex(
                name: "IX_OutboundClicks_ClickedUtc",
                table: "OutboundClicks",
                column: "ClickedUtc");

            migrationBuilder.CreateIndex(
                name: "IX_OutboundClicks_ConvertedUtc",
                table: "OutboundClicks",
                column: "ConvertedUtc");

            migrationBuilder.CreateIndex(
                name: "IX_OutboundClicks_ProductId",
                table: "OutboundClicks",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_OutboundClicks_ShopId_Campaign_Placement_ClickedUtc",
                table: "OutboundClicks",
                columns: new[] { "ShopId", "Campaign", "Placement", "ClickedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Products_IsEligible_LastSeenUtc",
                table: "Products",
                columns: new[] { "IsEligible", "LastSeenUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Products_LastRefreshedUtc",
                table: "Products",
                column: "LastRefreshedUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Products_SellerId",
                table: "Products",
                column: "SellerId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductSnapshots_FetchedUtc",
                table: "ProductSnapshots",
                column: "FetchedUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ProductSnapshots_ProductId_FetchedUtc",
                table: "ProductSnapshots",
                columns: new[] { "ProductId", "FetchedUtc" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShopProducts_ProductId",
                table: "ShopProducts",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ShopProducts_ShopId_IsActive_IsFeatured_DisplayOrder",
                table: "ShopProducts",
                columns: new[] { "ShopId", "IsActive", "IsFeatured", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_Shops_CanonicalHostname_PathPrefix",
                table: "Shops",
                columns: new[] { "CanonicalHostname", "PathPrefix" },
                unique: true,
                filter: "[CanonicalHostname] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Shops_IsEnabled",
                table: "Shops",
                column: "IsEnabled");

            migrationBuilder.CreateIndex(
                name: "IX_Shops_Slug",
                table: "Shops",
                column: "Slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AffiliateOrders");

            migrationBuilder.DropTable(
                name: "IngestionJobs");

            migrationBuilder.DropTable(
                name: "ProductSnapshots");

            migrationBuilder.DropTable(
                name: "ShopProducts");

            migrationBuilder.DropTable(
                name: "OutboundClicks");

            migrationBuilder.DropTable(
                name: "AffiliateLinks");

            migrationBuilder.DropTable(
                name: "Products");

            migrationBuilder.DropTable(
                name: "Shops");
        }
    }
}
