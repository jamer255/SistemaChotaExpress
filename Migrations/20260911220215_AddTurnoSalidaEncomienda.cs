using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SistemaChotaExpress.Migrations
{
    /// <inheritdoc />
    public partial class AddTurnoSalidaEncomienda : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Buses",
                columns: table => new
                {
                    Id_Bus = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Placa = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    NombreVehiculo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NombreConductor = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Modelo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Capacidad = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Buses", x => x.Id_Bus);
                });

            migrationBuilder.CreateTable(
                name: "Pasajeros",
                columns: table => new
                {
                    Id_Pasajero = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TipoDocumento = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    NumeroDocumento = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    NombreCompleto = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Telefono = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Correo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pasajeros", x => x.Id_Pasajero);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id_Rol = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NombreRol = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id_Rol);
                });

            migrationBuilder.CreateTable(
                name: "Rutas",
                columns: table => new
                {
                    Id_Ruta = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Origen = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Destino = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DuracionHoras = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rutas", x => x.Id_Ruta);
                });

            migrationBuilder.CreateTable(
                name: "Usuarios",
                columns: table => new
                {
                    Id_Usuario = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Password = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Telefono = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    Id_Rol = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Usuarios", x => x.Id_Usuario);
                    table.ForeignKey(
                        name: "FK_Usuarios_Roles_Id_Rol",
                        column: x => x.Id_Rol,
                        principalTable: "Roles",
                        principalColumn: "Id_Rol",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Viajes",
                columns: table => new
                {
                    Id_Viaje = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Id_Bus = table.Column<int>(type: "integer", nullable: true),
                    PlacaVehiculo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Id_Ruta = table.Column<int>(type: "integer", nullable: true),
                    NombreConductor = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    FechaHoraSalida = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Precio = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Viajes", x => x.Id_Viaje);
                    table.ForeignKey(
                        name: "FK_Viajes_Buses_Id_Bus",
                        column: x => x.Id_Bus,
                        principalTable: "Buses",
                        principalColumn: "Id_Bus");
                    table.ForeignKey(
                        name: "FK_Viajes_Rutas_Id_Ruta",
                        column: x => x.Id_Ruta,
                        principalTable: "Rutas",
                        principalColumn: "Id_Ruta",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Encomiendas",
                columns: table => new
                {
                    Id_Encomienda = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CodigoSeguimiento = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    NombreRemitente = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DniRemitente = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    TelefonoRemitente = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    NombreDestinatario = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DniDestinatario = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    TelefonoDestinatario = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    Descripcion = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PesoKg = table.Column<decimal>(type: "numeric(8,2)", nullable: true),
                    PrecioEnvio = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    Id_Ruta = table.Column<int>(type: "integer", nullable: true),
                    TurnoSalida = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Observaciones = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    FechaRegistro = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    FechaEntrega = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Id_Usuario = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Encomiendas", x => x.Id_Encomienda);
                    table.ForeignKey(
                        name: "FK_Encomiendas_Rutas_Id_Ruta",
                        column: x => x.Id_Ruta,
                        principalTable: "Rutas",
                        principalColumn: "Id_Ruta");
                    table.ForeignKey(
                        name: "FK_Encomiendas_Usuarios_Id_Usuario",
                        column: x => x.Id_Usuario,
                        principalTable: "Usuarios",
                        principalColumn: "Id_Usuario");
                });

            migrationBuilder.CreateTable(
                name: "Ventas",
                columns: table => new
                {
                    Id_Venta = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Id_Viaje = table.Column<int>(type: "integer", nullable: false),
                    Id_Pasajero = table.Column<int>(type: "integer", nullable: false),
                    Id_Usuario = table.Column<int>(type: "integer", nullable: false),
                    NumeroAsiento = table.Column<int>(type: "integer", nullable: false),
                    PrecioPagado = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    FechaVenta = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TipoComprobante = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    RucEmpresa = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    RazonSocialEmpresa = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DireccionEmpresa = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    MetodoPago = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Observaciones = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ventas", x => x.Id_Venta);
                    table.ForeignKey(
                        name: "FK_Ventas_Pasajeros_Id_Pasajero",
                        column: x => x.Id_Pasajero,
                        principalTable: "Pasajeros",
                        principalColumn: "Id_Pasajero",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Ventas_Usuarios_Id_Usuario",
                        column: x => x.Id_Usuario,
                        principalTable: "Usuarios",
                        principalColumn: "Id_Usuario",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Ventas_Viajes_Id_Viaje",
                        column: x => x.Id_Viaje,
                        principalTable: "Viajes",
                        principalColumn: "Id_Viaje",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Buses",
                columns: new[] { "Id_Bus", "Capacidad", "Modelo", "NombreConductor", "NombreVehiculo", "Placa" },
                values: new object[,]
                {
                    { 1, 15, "Toyota HiAce (Combi)", "Juan Pérez", "Combi 01", "S/P" },
                    { 2, 15, "Hyundai H350", "Carlos Gómez", "Combi 02", "S/P" },
                    { 3, 15, "Toyota HiAce (Combi)", "Mario Silva", "Combi 03", "S/P" },
                    { 4, 15, "Mercedes-Benz Sprinter", "Luis Fernández", "Combi 04", "S/P" }
                });

            migrationBuilder.InsertData(
                table: "Roles",
                columns: new[] { "Id_Rol", "NombreRol" },
                values: new object[,]
                {
                    { 1, "Gerente" },
                    { 2, "Vendedor" }
                });

            migrationBuilder.InsertData(
                table: "Rutas",
                columns: new[] { "Id_Ruta", "Destino", "DuracionHoras", "Origen" },
                values: new object[,]
                {
                    { 1, "Chota", 6.0, "Chiclayo" },
                    { 2, "Chiclayo", 6.0, "Chota" },
                    { 3, "Chota", 4.0, "Cajamarca" },
                    { 4, "Cajamarca", 4.0, "Chota" },
                    { 5, "Chiclayo", 7.0, "Cajamarca" },
                    { 6, "Cajamarca", 7.0, "Chiclayo" },
                    { 7, "Chota", 1.5, "Bambamarca" },
                    { 8, "Bambamarca", 1.5, "Chota" },
                    { 9, "Cutervo", 2.0, "Chota" },
                    { 10, "Chota", 2.0, "Cutervo" }
                });

            migrationBuilder.InsertData(
                table: "Usuarios",
                columns: new[] { "Id_Usuario", "Email", "Id_Rol", "Name", "Password", "Telefono" },
                values: new object[,]
                {
                    { 1, "gerente@chotaexpress.com", 1, "Gerente General", "gerente123", "976543210" },
                    { 2, "vendedor@chotaexpress.com", 2, "Juan Vendedor", "vendedor123", "987654321" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Encomiendas_Id_Ruta",
                table: "Encomiendas",
                column: "Id_Ruta");

            migrationBuilder.CreateIndex(
                name: "IX_Encomiendas_Id_Usuario",
                table: "Encomiendas",
                column: "Id_Usuario");

            migrationBuilder.CreateIndex(
                name: "IX_Pasajeros_TipoDocumento_NumeroDocumento",
                table: "Pasajeros",
                columns: new[] { "TipoDocumento", "NumeroDocumento" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_Id_Rol",
                table: "Usuarios",
                column: "Id_Rol");

            migrationBuilder.CreateIndex(
                name: "IX_Ventas_Id_Pasajero",
                table: "Ventas",
                column: "Id_Pasajero");

            migrationBuilder.CreateIndex(
                name: "IX_Ventas_Id_Usuario",
                table: "Ventas",
                column: "Id_Usuario");

            migrationBuilder.CreateIndex(
                name: "IX_Ventas_Id_Viaje",
                table: "Ventas",
                column: "Id_Viaje");

            migrationBuilder.CreateIndex(
                name: "IX_Viajes_Id_Bus",
                table: "Viajes",
                column: "Id_Bus");

            migrationBuilder.CreateIndex(
                name: "IX_Viajes_Id_Ruta",
                table: "Viajes",
                column: "Id_Ruta");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Encomiendas");

            migrationBuilder.DropTable(
                name: "Ventas");

            migrationBuilder.DropTable(
                name: "Pasajeros");

            migrationBuilder.DropTable(
                name: "Usuarios");

            migrationBuilder.DropTable(
                name: "Viajes");

            migrationBuilder.DropTable(
                name: "Roles");

            migrationBuilder.DropTable(
                name: "Buses");

            migrationBuilder.DropTable(
                name: "Rutas");
        }
    }
}
