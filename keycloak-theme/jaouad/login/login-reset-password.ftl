<#import "template.ftl" as layout>
<@layout.registrationLayout displayMessage=true displayInfo=true showBackToLogin=true; section>
    <#if section = "header">
        ${msg("emailForgotTitle")}
    <#elseif section = "form">
        <div class="auth-form-section">
            <h1 class="auth-title">Forgot your password?</h1>
            <p class="auth-subtitle">Enter your email and we'll send you a reset link.</p>


            <form id="kc-reset-password-form" action="${url.loginAction}" method="post">
                <div class="auth-field">
                    <label for="username"><#if !realm.loginWithEmailAllowed>${msg("username")}<#elseif !realm.registrationEmailAsUsername>${msg("usernameOrEmail")}<#else>${msg("email")}</#if></label>
                    <input type="text" id="username" name="username" autofocus value="${(auth.attemptedUsername!'')}" placeholder="name@company.com" />
                </div>
                <button class="auth-btn-primary" type="submit">Send reset link</button>
            </form>
        </div>

    <#elseif section = "info">
        <div class="auth-switch">
            <span>Remember your password? <a href="${url.loginUrl}">Back to sign in</a></span>
        </div>
    </#if>
</@layout.registrationLayout>
