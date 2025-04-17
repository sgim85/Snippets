using System.Text;
using System.Text.RegularExpressions;
using WkHtmlToPdfDotNet;

namespace CXP.Utilities
{
    public partial class Utils
    {
        /*
            Converts an html string to pdf using the HtmlToPdfDocument library.
            If running via a Container, update Dockerfile with directives to install WkHtmlToPdfDotNet libraries in the container. E.g. See the WkHtmlToPdfDotNet block in the sample Dockerfile content below

            #See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

            FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base

            # WkHtmlToPdfDotNet
            #### Install dependencies for the html-to-pdf library WkHtmlToPdf-DotNet. Source: https://github.com/HakanL/WkHtmlToPdf-DotNet, https://github.com/HakanL/WkHtmlToPdf-DotNet/issues/121 ####
            RUN apt-get update && ln -s /usr/lib/libgdiplus.so /lib/x86_64-linux-gnu/libgdiplus.so
            RUN apt-get install -y --no-install-recommends zlib1g fontconfig libfreetype6 libx11-6 libxext6 libxrender1 wget gdebi
            RUN wget https://github.com/wkhtmltopdf/wkhtmltopdf/releases/download/0.12.5/wkhtmltox_0.12.5-1.stretch_amd64.deb
            RUN wget http://archive.ubuntu.com/ubuntu/pool/main/o/openssl/libssl1.1_1.1.1f-1ubuntu2_amd64.deb
            RUN dpkg -i libssl1.1_1.1.1f-1ubuntu2_amd64.deb
            RUN gdebi --n wkhtmltox_0.12.5-1.stretch_amd64.deb
            RUN ln -s /usr/local/lib/libwkhtmltox.so /usr/lib/libwkhtmltox.so
            #### ---------------------------------- ####

            WORKDIR /app
            EXPOSE 80
            EXPOSE 443

            FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
            WORKDIR /src
            #COPY ["API/Profile.API/Profile.API.csproj", "Profile.API/"]
            COPY . .
            RUN dotnet restore "API/Profile.API/Profile.API.csproj"
            COPY . .
            WORKDIR "/src/API/Profile.API"
            RUN dotnet build "Profile.API.csproj" -c Release -o /app/build

            FROM build AS publish
            RUN dotnet publish "Profile.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

            FROM base AS final
            WORKDIR /app
            COPY --from=publish /app/publish .
            ENTRYPOINT ["dotnet", "Profile.API.dll"]
        */
        public byte[] ConvertHtmlToPdfBytes(string htmlBody)
        {
            // CXP templates don't contain the top level html elements (<html>,<head>,title>,<body>) because they are optimized for the cxp inbox
            // When generating pdfs we need to add these top level html elements
            if (htmlBody != null && (Regex.Match(htmlBody, "<\\/?html>", RegexOptions.IgnoreCase).Success == false || Regex.IsMatch(htmlBody, "<\\/?html>", RegexOptions.IgnoreCase) == false))
            {
                htmlBody = $"<html><head><title></title></head><body>{htmlBody}</body></html>";
            }

            var doc = new HtmlToPdfDocument()
            {
                GlobalSettings = {
                        ColorMode = ColorMode.Color,
                        Orientation = Orientation.Portrait,
                        PaperSize = PaperKind.A4
                    },
                Objects = {
                            new ObjectSettings() {
                                PagesCount = true,
                                HtmlContent = htmlBody,
                                WebSettings = { DefaultEncoding = "utf-8" },
                                Encoding = Encoding.UTF8
                                //HeaderSettings = { FontSize = 12 }
                            }
                        }
            };

            var bytes = _synchConverter.Convert(doc);
            return bytes;
        }

        public string ConvertHtmlToPdfBase64(string htmlBody)
        {
            var bytes = ConvertHtmlToPdfBytes(htmlBody);
            var pdfBase64 = Convert.ToBase64String(bytes);
            return pdfBase64;
        }
    }
}