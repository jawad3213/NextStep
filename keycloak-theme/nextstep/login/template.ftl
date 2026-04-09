<#macro registrationLayout bodyClass="" displayInfo=false displayMessage=true displayRequiredFields=false showBackToLogin=false>
<!DOCTYPE html>
<html lang="${properties.kcHtmlLanguage!}">

<head>
    <meta charset="utf-8">
    <meta http-equiv="Content-Type" content="text/html; charset=UTF-8" />
    <meta name="robots" content="noindex, nofollow">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>${msg("loginTitle", (realm.displayName!''))}</title>
    
    <link href="https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700;800&display=swap" rel="stylesheet">
    <link rel="stylesheet" href="https://cdnjs.cloudflare.com/ajax/libs/font-awesome/6.4.0/css/all.min.css">
    
    <#if properties.stylesCommon?has_content>
        <#list properties.stylesCommon?split(' ') as style>
            <link href="${url.resourcesCommonPath}/${style}" rel="stylesheet" />
        </#list>
    </#if>
    <#if properties.styles?has_content>
        <#list properties.styles?split(' ') as style>
            <link href="${url.resourcesPath}/${style}?v=6" rel="stylesheet" />
        </#list>
    </#if>
</head>

<body class="${bodyClass}">
    <div class="login-container">
        
        <!-- Left Hero Section -->
        <div class="hero-section">
            <div class="hero-badge">Join the Elite</div>
            <h1 class="hero-title">Start your journey<br>with <span>AI-powered</span><br>career growth.</h1>
            <p class="hero-subtitle">NextStep uses proprietary kinetic algorithms to align your unique professional signature with the world's most innovative opportunities.</p>
            
            <div class="stats-container">
                <div class="stat-box">
                    <div class="stat-number">98%</div>
                    <div class="stat-label">Match Accuracy</div>
                </div>
                <div class="stat-box">
                    <div class="stat-number">2.4k+</div>
                    <div class="stat-label">Hiring Partners</div>
                </div>
            </div>

            <div class="trust-badges">
                <div class="trust-badge"><div class="trust-badge-dot"></div>SOC 2 certified</div>
                <div class="trust-badge"><div class="trust-badge-dot"></div>GDPR compliant</div>
                <div class="trust-badge"><div class="trust-badge-dot"></div>256-bit SSL</div>
            </div>
        </div>

        <!-- Right Form Section -->
        <div class="form-section">
            <div class="form-wrapper">
                <div class="form-header">
                    <#if showBackToLogin>
                        <a href="${url.loginUrl}" class="back-link">
                            <i class="fa-solid fa-arrow-left"></i> Back to Sign In
                        </a>
                    </#if>

                    <h2 class="form-title">
                        <#if realm.displayNameHtml?has_content>
                            ${realm.displayNameHtml?no_esc}
                        <#else>
                            NextStep Access
                        </#if>
                    </h2>
                    <p class="form-subtitle">
                        <#if msg("loginAccountTitle")?has_content>
                            Get started with your professional ascent today.
                        <#else>
                            ${msg("loginAccountTitle")}
                        </#if>
                    </p>
                </div>

                <#if displayMessage && message?has_content && (message.type != 'warning' || !isAppInitiatedAction??)>
                    <div class="alert alert-${message.type}">
                        <#if message.type == 'success'><span class="fa fa-check-circle"></span></#if>
                        <#if message.type == 'warning'><span class="fa fa-exclamation-triangle"></span></#if>
                        <#if message.type == 'error'><span class="fa fa-exclamation-circle"></span></#if>
                        <#if message.type == 'info'><span class="fa fa-info-circle"></span></#if>
                        <span class="kc-feedback-text">${kcSanitize(message.summary)?no_esc}</span>
                    </div>
                </#if>

                <#nested "form">

                <#if displayInfo>
                    <div class="form-footer">
                        <#nested "info">
                    </div>
                </#if>
            </div>
        </div>
        
    </div>
</body>
</html>
</#macro>
