import type { Metadata } from "next";
import { Archivo } from "next/font/google";
import "./globals.css";

// Una sola familia: Archivo. Los titulos usan su eje de ancho (expandido), como la rotulacion de un plano.
const archivo = Archivo({
  variable: "--font-archivo",
  subsets: ["latin"],
  axes: ["wdth"],
});

export const metadata: Metadata = {
  title: "IAProyect - Diseno de interiores",
  description: "Disena tu espacio sobre medidas reales: editor 3D, propuestas con IA y presupuesto en pesos.",
};

export default function RootLayout({ children }: LayoutProps<"/">) {
  return (
    <html lang="es" className={archivo.variable}>
      <body>{children}</body>
    </html>
  );
}
