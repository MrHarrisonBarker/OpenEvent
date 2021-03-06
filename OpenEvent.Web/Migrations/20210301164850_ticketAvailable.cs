﻿using Microsoft.EntityFrameworkCore.Migrations;

namespace OpenEvent.Web.Migrations
{
#pragma warning disable CS1591
    public partial class ticketAvailable : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Available",
                table: "Tickets",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Available",
                table: "Tickets");
        }
    }
#pragma warning restore CS1591
}
