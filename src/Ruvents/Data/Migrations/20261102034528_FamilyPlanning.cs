using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ruvents.Migrations;

/// <inheritdoc />
internal sealed partial class FamilyPlanning : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "DisplayName",
            table: "AspNetUsers",
            type: "character varying(100)",
            maxLength: 100,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "ProfilePhotoBlobName",
            table: "AspNetUsers",
            type: "character varying(300)",
            maxLength: 300,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ProfilePhotoContentType",
            table: "AspNetUsers",
            type: "character varying(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "SuspendedAtUtc",
            table: "AspNetUsers",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "SuspendedReason",
            table: "AspNetUsers",
            type: "character varying(500)",
            maxLength: 500,
            nullable: true);

        migrationBuilder.CreateTable(
            name: "FamilyGroups",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                CreatedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_FamilyGroups", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "HouseholdMembers",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_HouseholdMembers", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Notifications",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                Kind = table.Column<int>(type: "integer", nullable: false),
                Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Body = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                LinkUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ReadAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Notifications", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Events",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                FamilyGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                LocationName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                LocationAddress = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                MapUrl = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                StartUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                EndUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                IsAllDay = table.Column<bool>(type: "boolean", nullable: false),
                TimeZoneId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                RsvpDeadlineUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CoverImageBlobName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                CoverImageContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                PotluckEnabled = table.Column<bool>(type: "boolean", nullable: false),
                Status = table.Column<int>(type: "integer", nullable: false),
                CreatedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Events", x => x.Id);
                table.ForeignKey(
                    name: "FK_Events_FamilyGroups_FamilyGroupId",
                    column: x => x.FamilyGroupId,
                    principalTable: "FamilyGroups",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "FamilyJoinRequests",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                FamilyGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                Status = table.Column<int>(type: "integer", nullable: false),
                Message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                RequestedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                DecidedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                DecidedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_FamilyJoinRequests", x => x.Id);
                table.ForeignKey(
                    name: "FK_FamilyJoinRequests_FamilyGroups_FamilyGroupId",
                    column: x => x.FamilyGroupId,
                    principalTable: "FamilyGroups",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "FamilyMemberships",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                FamilyGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                Role = table.Column<int>(type: "integer", nullable: false),
                JoinedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_FamilyMemberships", x => x.Id);
                table.ForeignKey(
                    name: "FK_FamilyMemberships_FamilyGroups_FamilyGroupId",
                    column: x => x.FamilyGroupId,
                    principalTable: "FamilyGroups",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "EventPosts",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                EventId = table.Column<Guid>(type: "uuid", nullable: false),
                ParentPostId = table.Column<Guid>(type: "uuid", nullable: true),
                AuthorUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                Kind = table.Column<int>(type: "integer", nullable: false),
                Body = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                DeletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_EventPosts", x => x.Id);
                table.ForeignKey(
                    name: "FK_EventPosts_EventPosts_ParentPostId",
                    column: x => x.ParentPostId,
                    principalTable: "EventPosts",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_EventPosts_Events_EventId",
                    column: x => x.EventId,
                    principalTable: "Events",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "EventRsvps",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                EventId = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                IsAttending = table.Column<bool>(type: "boolean", nullable: false),
                Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                RespondedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_EventRsvps", x => x.Id);
                table.ForeignKey(
                    name: "FK_EventRsvps_Events_EventId",
                    column: x => x.EventId,
                    principalTable: "Events",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "PotluckSlots",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                EventId = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                QuantityNeeded = table.Column<int>(type: "integer", nullable: false),
                IsCustom = table.Column<bool>(type: "boolean", nullable: false),
                CreatedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                SortOrder = table.Column<int>(type: "integer", nullable: false),
                ClaimVersion = table.Column<int>(type: "integer", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PotluckSlots", x => x.Id);
                table.ForeignKey(
                    name: "FK_PotluckSlots_Events_EventId",
                    column: x => x.EventId,
                    principalTable: "Events",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "EventRsvpGuests",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                EventRsvpId = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_EventRsvpGuests", x => x.Id);
                table.ForeignKey(
                    name: "FK_EventRsvpGuests_EventRsvps_EventRsvpId",
                    column: x => x.EventRsvpId,
                    principalTable: "EventRsvps",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "EventRsvpHouseholdMembers",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                EventRsvpId = table.Column<Guid>(type: "uuid", nullable: false),
                HouseholdMemberId = table.Column<Guid>(type: "uuid", nullable: false),
                IsAttending = table.Column<bool>(type: "boolean", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_EventRsvpHouseholdMembers", x => x.Id);
                table.ForeignKey(
                    name: "FK_EventRsvpHouseholdMembers_EventRsvps_EventRsvpId",
                    column: x => x.EventRsvpId,
                    principalTable: "EventRsvps",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_EventRsvpHouseholdMembers_HouseholdMembers_HouseholdMemberId",
                    column: x => x.HouseholdMemberId,
                    principalTable: "HouseholdMembers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "PotluckClaims",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                PotluckSlotId = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                Quantity = table.Column<int>(type: "integer", nullable: false),
                DishNote = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                AssignedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                ClaimedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PotluckClaims", x => x.Id);
                table.ForeignKey(
                    name: "FK_PotluckClaims_PotluckSlots_PotluckSlotId",
                    column: x => x.PotluckSlotId,
                    principalTable: "PotluckSlots",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_EventPosts_EventId_CreatedAtUtc",
            table: "EventPosts",
            columns: ["EventId", "CreatedAtUtc"]);

        migrationBuilder.CreateIndex(
            name: "IX_EventPosts_ParentPostId",
            table: "EventPosts",
            column: "ParentPostId");

        migrationBuilder.CreateIndex(
            name: "IX_EventRsvpGuests_EventRsvpId",
            table: "EventRsvpGuests",
            column: "EventRsvpId");

        migrationBuilder.CreateIndex(
            name: "IX_EventRsvpHouseholdMembers_EventRsvpId_HouseholdMemberId",
            table: "EventRsvpHouseholdMembers",
            columns: ["EventRsvpId", "HouseholdMemberId"],
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_EventRsvpHouseholdMembers_HouseholdMemberId",
            table: "EventRsvpHouseholdMembers",
            column: "HouseholdMemberId");

        migrationBuilder.CreateIndex(
            name: "IX_EventRsvps_EventId_UserId",
            table: "EventRsvps",
            columns: ["EventId", "UserId"],
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Events_FamilyGroupId_StartUtc",
            table: "Events",
            columns: ["FamilyGroupId", "StartUtc"]);

        migrationBuilder.CreateIndex(
            name: "IX_FamilyGroups_Name",
            table: "FamilyGroups",
            column: "Name");

        migrationBuilder.CreateIndex(
            name: "IX_FamilyJoinRequests_FamilyGroupId_Status",
            table: "FamilyJoinRequests",
            columns: ["FamilyGroupId", "Status"]);

        migrationBuilder.CreateIndex(
            name: "IX_FamilyJoinRequests_FamilyGroupId_UserId",
            table: "FamilyJoinRequests",
            columns: ["FamilyGroupId", "UserId"],
            unique: true,
            filter: "\"Status\" = 0");

        migrationBuilder.CreateIndex(
            name: "IX_FamilyMemberships_FamilyGroupId_UserId",
            table: "FamilyMemberships",
            columns: ["FamilyGroupId", "UserId"],
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_FamilyMemberships_UserId",
            table: "FamilyMemberships",
            column: "UserId");

        migrationBuilder.CreateIndex(
            name: "IX_HouseholdMembers_UserId",
            table: "HouseholdMembers",
            column: "UserId");

        migrationBuilder.CreateIndex(
            name: "IX_Notifications_UserId_ReadAtUtc",
            table: "Notifications",
            columns: ["UserId", "ReadAtUtc"]);

        migrationBuilder.CreateIndex(
            name: "IX_PotluckClaims_PotluckSlotId_UserId",
            table: "PotluckClaims",
            columns: ["PotluckSlotId", "UserId"],
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_PotluckSlots_EventId_SortOrder",
            table: "PotluckSlots",
            columns: ["EventId", "SortOrder"]);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "EventPosts");

        migrationBuilder.DropTable(
            name: "EventRsvpGuests");

        migrationBuilder.DropTable(
            name: "EventRsvpHouseholdMembers");

        migrationBuilder.DropTable(
            name: "FamilyJoinRequests");

        migrationBuilder.DropTable(
            name: "FamilyMemberships");

        migrationBuilder.DropTable(
            name: "Notifications");

        migrationBuilder.DropTable(
            name: "PotluckClaims");

        migrationBuilder.DropTable(
            name: "EventRsvps");

        migrationBuilder.DropTable(
            name: "HouseholdMembers");

        migrationBuilder.DropTable(
            name: "PotluckSlots");

        migrationBuilder.DropTable(
            name: "Events");

        migrationBuilder.DropTable(
            name: "FamilyGroups");

        migrationBuilder.DropColumn(
            name: "DisplayName",
            table: "AspNetUsers");

        migrationBuilder.DropColumn(
            name: "ProfilePhotoBlobName",
            table: "AspNetUsers");

        migrationBuilder.DropColumn(
            name: "ProfilePhotoContentType",
            table: "AspNetUsers");

        migrationBuilder.DropColumn(
            name: "SuspendedAtUtc",
            table: "AspNetUsers");

        migrationBuilder.DropColumn(
            name: "SuspendedReason",
            table: "AspNetUsers");
    }
}
