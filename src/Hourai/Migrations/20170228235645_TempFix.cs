using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Hourai.Migrations
{
    public partial class TempFix : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Prefix",
                table: "guilds",
                maxLength: 1,
                nullable: true,
                defaultValue: "~",
                oldClrType: typeof(string),
                oldMaxLength: 1,
                oldDefaultValue: "~")
                .OldAnnotation("MySql:ValueGeneratedOnAdd", true);

            migrationBuilder.AlterColumn<long>(
                name: "Id",
                table: "temp_actions",
                nullable: false,
                oldClrType: typeof(ulong))
                .Annotation("MySql:ValueGeneratedOnAdd", true)
                .OldAnnotation("MySql:ValueGeneratedOnAdd", true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Prefix",
                table: "guilds",
                maxLength: 1,
                nullable: false,
                defaultValue: "~",
                oldClrType: typeof(string),
                oldMaxLength: 1,
                oldNullable: true,
                oldDefaultValue: "~")
                .Annotation("MySql:ValueGeneratedOnAdd", true);

            migrationBuilder.AlterColumn<ulong>(
                name: "Id",
                table: "temp_actions",
                nullable: false,
                oldClrType: typeof(long))
                .Annotation("MySql:ValueGeneratedOnAdd", true)
                .OldAnnotation("MySql:ValueGeneratedOnAdd", true);
        }
    }
}
