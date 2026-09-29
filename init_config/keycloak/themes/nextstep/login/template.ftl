<#macro registrationLayout bodyClass="" customLayout=false displayInfo=false displayMessage=true displayRequiredFields=false showBackToLogin=false>
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

            <#-- Pages customised in this theme (customLayout=true) draw their own
                 title and messages inside the "form" section. Every other page is
                 inherited from the base Keycloak theme (account linking, Google
                 profile review, re-authentication, errors...) and needs its header
                 and message rendered here, otherwise it shows bare buttons. -->
            <#if customLayout>
                <#nested "form">
            <#else>
                <div class="auth-form-section kc-generic">
                    <h1 class="auth-title"><#nested "header"></h1>
                    <#if displayMessage && message?has_content && (message.type != 'warning' || !isAppInitiatedAction??)>
                        <div class="alert alert-${message.type}">
                            <span>${kcSanitize(message.summary)?no_esc}</span>
                        </div>
                    </#if>
                    <#nested "form">
                </div>
            </#if>

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
