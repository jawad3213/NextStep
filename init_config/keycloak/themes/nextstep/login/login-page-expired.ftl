<#import "template.ftl" as layout>
<@layout.registrationLayout; section>
    <#if section = "header">
        ${msg("pageExpiredTitle")}
    <#elseif section = "form">
        <div class="auth-form-section" style="text-align: center;">
            <h1 class="auth-title">Session Expired</h1>
            <p class="auth-subtitle">Your login session has timed out for security reasons.</p>
            
            <div style="margin-top: 32px;">
                <p class="auth-subtitle">
                    ${msg("pageExpiredMsg1")} <a id="loginRestartLink" href="${url.loginRestartFlowUrl}" class="auth-btn-primary" style="display: inline-block; text-decoration: none; margin-top: 12px;">${msg("doClickHere")}</a>
                </p>
                <p class="auth-subtitle" style="margin-top: 16px;">
                    ${msg("pageExpiredMsg2")} <a id="loginContinueLink" href="${url.loginAction}" class="auth-link-teal">${msg("doClickHere")}</a>
                </p>
            </div>
        </div>
    </#if>
</@layout.registrationLayout>
