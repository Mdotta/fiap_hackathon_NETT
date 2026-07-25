using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Solidary.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ChangeCampaignDatesToDateTimeOffset : Migration
    {
        // No-op at the SQL level: Campaign.StartDate/EndDate moved from DateTime to DateTimeOffset in the
        // domain model (fixing a request-body offset-parsing bug), but Npgsql already mapped DateTime to
        // "timestamp with time zone" by default, so the column type is unchanged. This migration exists
        // solely to keep SolidaryDbContextModelSnapshot.cs in sync with the real CLR type.
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
