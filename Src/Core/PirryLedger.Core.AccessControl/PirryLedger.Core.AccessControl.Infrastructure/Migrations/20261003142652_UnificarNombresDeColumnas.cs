using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PirryLedger.Core.AccessControl.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UnificarNombresDeColumnas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ac_codigos_recuperacion_ac_usuarios_UsuarioId",
                table: "ac_codigos_recuperacion");

            migrationBuilder.DropForeignKey(
                name: "FK_ac_sesiones_ac_usuarios_UsuarioId",
                table: "ac_sesiones");

            migrationBuilder.DropForeignKey(
                name: "FK_ac_tokens_activacion_ac_usuarios_UsuarioId",
                table: "ac_tokens_activacion");

            migrationBuilder.RenameColumn(
                name: "Rol",
                table: "ac_usuarios",
                newName: "rol");

            migrationBuilder.RenameColumn(
                name: "Nombre",
                table: "ac_usuarios",
                newName: "nombre");

            migrationBuilder.RenameColumn(
                name: "Correo",
                table: "ac_usuarios",
                newName: "correo");

            migrationBuilder.RenameColumn(
                name: "Activo",
                table: "ac_usuarios",
                newName: "activo");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "ac_usuarios",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "IntentosFallidos",
                table: "ac_usuarios",
                newName: "intentos_fallidos");

            migrationBuilder.RenameColumn(
                name: "HashDeContrasena",
                table: "ac_usuarios",
                newName: "hash_de_contrasena");

            migrationBuilder.RenameColumn(
                name: "FechaDeCreacionUtc",
                table: "ac_usuarios",
                newName: "fecha_de_creacion_utc");

            migrationBuilder.RenameColumn(
                name: "CredencialVersion",
                table: "ac_usuarios",
                newName: "credencial_version");

            migrationBuilder.RenameColumn(
                name: "ContrasenaCambiadaUtc",
                table: "ac_usuarios",
                newName: "contrasena_cambiada_utc");

            migrationBuilder.RenameColumn(
                name: "BloqueoHastaUtc",
                table: "ac_usuarios",
                newName: "bloqueo_hasta_utc");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "ac_tokens_activacion",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "UsuarioId",
                table: "ac_tokens_activacion",
                newName: "usuario_id");

            migrationBuilder.RenameColumn(
                name: "UsadoUtc",
                table: "ac_tokens_activacion",
                newName: "usado_utc");

            migrationBuilder.RenameColumn(
                name: "HashDelToken",
                table: "ac_tokens_activacion",
                newName: "hash_del_token");

            migrationBuilder.RenameColumn(
                name: "ExpiraUtc",
                table: "ac_tokens_activacion",
                newName: "expira_utc");

            migrationBuilder.RenameColumn(
                name: "EmitidoUtc",
                table: "ac_tokens_activacion",
                newName: "emitido_utc");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "ac_sesiones",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "UsuarioId",
                table: "ac_sesiones",
                newName: "usuario_id");

            migrationBuilder.RenameColumn(
                name: "HashDelToken",
                table: "ac_sesiones",
                newName: "hash_del_token");

            migrationBuilder.RenameColumn(
                name: "ExpiraUtc",
                table: "ac_sesiones",
                newName: "expira_utc");

            migrationBuilder.RenameColumn(
                name: "EmitidaUtc",
                table: "ac_sesiones",
                newName: "emitida_utc");

            migrationBuilder.RenameColumn(
                name: "CredencialVersion",
                table: "ac_sesiones",
                newName: "credencial_version");

            migrationBuilder.RenameColumn(
                name: "CerradaUtc",
                table: "ac_sesiones",
                newName: "cerrada_utc");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "ac_codigos_recuperacion",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "UsuarioId",
                table: "ac_codigos_recuperacion",
                newName: "usuario_id");

            migrationBuilder.RenameColumn(
                name: "UsadoUtc",
                table: "ac_codigos_recuperacion",
                newName: "usado_utc");

            migrationBuilder.RenameColumn(
                name: "HashDelCodigo",
                table: "ac_codigos_recuperacion",
                newName: "hash_del_codigo");

            migrationBuilder.RenameColumn(
                name: "ExpiraUtc",
                table: "ac_codigos_recuperacion",
                newName: "expira_utc");

            migrationBuilder.RenameColumn(
                name: "EmitidoUtc",
                table: "ac_codigos_recuperacion",
                newName: "emitido_utc");

            migrationBuilder.AddForeignKey(
                name: "FK_ac_codigos_recuperacion_ac_usuarios_usuario_id",
                table: "ac_codigos_recuperacion",
                column: "usuario_id",
                principalTable: "ac_usuarios",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ac_sesiones_ac_usuarios_usuario_id",
                table: "ac_sesiones",
                column: "usuario_id",
                principalTable: "ac_usuarios",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ac_tokens_activacion_ac_usuarios_usuario_id",
                table: "ac_tokens_activacion",
                column: "usuario_id",
                principalTable: "ac_usuarios",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ac_codigos_recuperacion_ac_usuarios_usuario_id",
                table: "ac_codigos_recuperacion");

            migrationBuilder.DropForeignKey(
                name: "FK_ac_sesiones_ac_usuarios_usuario_id",
                table: "ac_sesiones");

            migrationBuilder.DropForeignKey(
                name: "FK_ac_tokens_activacion_ac_usuarios_usuario_id",
                table: "ac_tokens_activacion");

            migrationBuilder.RenameColumn(
                name: "rol",
                table: "ac_usuarios",
                newName: "Rol");

            migrationBuilder.RenameColumn(
                name: "nombre",
                table: "ac_usuarios",
                newName: "Nombre");

            migrationBuilder.RenameColumn(
                name: "correo",
                table: "ac_usuarios",
                newName: "Correo");

            migrationBuilder.RenameColumn(
                name: "activo",
                table: "ac_usuarios",
                newName: "Activo");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "ac_usuarios",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "intentos_fallidos",
                table: "ac_usuarios",
                newName: "IntentosFallidos");

            migrationBuilder.RenameColumn(
                name: "hash_de_contrasena",
                table: "ac_usuarios",
                newName: "HashDeContrasena");

            migrationBuilder.RenameColumn(
                name: "fecha_de_creacion_utc",
                table: "ac_usuarios",
                newName: "FechaDeCreacionUtc");

            migrationBuilder.RenameColumn(
                name: "credencial_version",
                table: "ac_usuarios",
                newName: "CredencialVersion");

            migrationBuilder.RenameColumn(
                name: "contrasena_cambiada_utc",
                table: "ac_usuarios",
                newName: "ContrasenaCambiadaUtc");

            migrationBuilder.RenameColumn(
                name: "bloqueo_hasta_utc",
                table: "ac_usuarios",
                newName: "BloqueoHastaUtc");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "ac_tokens_activacion",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "usuario_id",
                table: "ac_tokens_activacion",
                newName: "UsuarioId");

            migrationBuilder.RenameColumn(
                name: "usado_utc",
                table: "ac_tokens_activacion",
                newName: "UsadoUtc");

            migrationBuilder.RenameColumn(
                name: "hash_del_token",
                table: "ac_tokens_activacion",
                newName: "HashDelToken");

            migrationBuilder.RenameColumn(
                name: "expira_utc",
                table: "ac_tokens_activacion",
                newName: "ExpiraUtc");

            migrationBuilder.RenameColumn(
                name: "emitido_utc",
                table: "ac_tokens_activacion",
                newName: "EmitidoUtc");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "ac_sesiones",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "usuario_id",
                table: "ac_sesiones",
                newName: "UsuarioId");

            migrationBuilder.RenameColumn(
                name: "hash_del_token",
                table: "ac_sesiones",
                newName: "HashDelToken");

            migrationBuilder.RenameColumn(
                name: "expira_utc",
                table: "ac_sesiones",
                newName: "ExpiraUtc");

            migrationBuilder.RenameColumn(
                name: "emitida_utc",
                table: "ac_sesiones",
                newName: "EmitidaUtc");

            migrationBuilder.RenameColumn(
                name: "credencial_version",
                table: "ac_sesiones",
                newName: "CredencialVersion");

            migrationBuilder.RenameColumn(
                name: "cerrada_utc",
                table: "ac_sesiones",
                newName: "CerradaUtc");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "ac_codigos_recuperacion",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "usuario_id",
                table: "ac_codigos_recuperacion",
                newName: "UsuarioId");

            migrationBuilder.RenameColumn(
                name: "usado_utc",
                table: "ac_codigos_recuperacion",
                newName: "UsadoUtc");

            migrationBuilder.RenameColumn(
                name: "hash_del_codigo",
                table: "ac_codigos_recuperacion",
                newName: "HashDelCodigo");

            migrationBuilder.RenameColumn(
                name: "expira_utc",
                table: "ac_codigos_recuperacion",
                newName: "ExpiraUtc");

            migrationBuilder.RenameColumn(
                name: "emitido_utc",
                table: "ac_codigos_recuperacion",
                newName: "EmitidoUtc");

            migrationBuilder.AddForeignKey(
                name: "FK_ac_codigos_recuperacion_ac_usuarios_UsuarioId",
                table: "ac_codigos_recuperacion",
                column: "UsuarioId",
                principalTable: "ac_usuarios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ac_sesiones_ac_usuarios_UsuarioId",
                table: "ac_sesiones",
                column: "UsuarioId",
                principalTable: "ac_usuarios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ac_tokens_activacion_ac_usuarios_UsuarioId",
                table: "ac_tokens_activacion",
                column: "UsuarioId",
                principalTable: "ac_usuarios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
