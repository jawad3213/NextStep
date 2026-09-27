<#macro registrationLayout bodyClass="" displayInfo=false displayMessage=true displayRequiredFields=false showBackToLogin=false>
<!DOCTYPE html>
<html lang="${properties.kcHtmlLanguage!}">
<head>
    <meta charset="utf-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>${msg("loginTitle", (realm.displayName!''))}</title>
    <link href="https://fonts.googleapis.com/css2?family=Inter:wght@400;600;700&display=swap" rel="stylesheet">
    <#if properties.styles?has_content>
        <#list properties.styles?split(' ') as style>
            <link href="${url.resourcesPath}/${style}" rel="stylesheet" />
        </#list>
    </#if>
</head>
<body class="${bodyClass}">
    <div class="auth-page">
        <div class="auth-container">
            <!-- Official NextStep Logo -->
            <div class="auth-logo">
                <img src="${url.resourcesPath}/img/logo.png" alt="NextStep Logo" class="auth-logo-img" />
            </div>

            <#nested "form">

            <#if displayInfo>
                <#nested "info">
            </#if>
        </div>
    </div>

    <!-- Fixed Footer at Bottom -->
    <div class="footer-legal">
        <div class="copyright">&copy; ${.now?string('yyyy')} NextStep Inc. All rights reserved.</div>
        <div class="footer-links">
            <a href="#">Terms of Service</a>
            <a href="#">Privacy Policy</a>
        </div>
    </div>

    <script src="https://unpkg.com/feather-icons"></script>
    <script>
        if (typeof feather !== 'undefined') {
            feather.replace({ width: 16, height: 16 });
        }
    </script>
</body>
</html>
</#macro>
