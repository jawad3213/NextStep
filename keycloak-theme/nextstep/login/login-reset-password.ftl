<#import "template.ftl" as layout>
<@layout.registrationLayout displayMessage=!messagesPerField.existsError('username'); section>
    <#if section = "header">
        ${msg("emailForgotTitle")}
    <#elseif section = "form">
        <form id="kc-reset-password-form" class="form" action="${url.loginAction}" method="post">
            <div class="form-group" style="margin-bottom: 2rem;">
                <label for="username"><#if !realm.loginWithEmailAllowed>${msg("username")}<#elseif !realm.registrationEmailAsUsername>${msg("usernameOrEmail")}<#else>${msg("email")}</#if></label>
                <div class="input-wrapper">
                    <i class="fa fa-envelope input-icon"></i>
                    <input type="text" id="username" name="username" class="" autofocus value="${(auth.attemptedUsername!'')}" aria-invalid="<#if messagesPerField.existsError('username')>true</#if>"/>
                </div>
                <#if messagesPerField.existsError('username')>
                    <span id="input-error-username" class="error-text" aria-live="polite">
                        ${kcSanitize(messagesPerField.get('username'))?no_esc}
                    </span>
                </#if>
            </div>

            <div class="form-group">
                <button class="btn-primary" type="submit">${msg("doSubmit")}</button>
            </div>
            
        </form>
    <#elseif section = "info" >
        <div id="kc-info-message">
            <p style="text-align: center; color: var(--text-light); font-size: 0.9rem; margin-bottom: 1rem;">
                Enter your email address and we'll send you instructions on how to reset your password.
            </p>
            <div id="kc-registration">
                <span><a href="${url.loginUrl}">${msg("backToLogin")}</a></span>
            </div>
        </div>
    </#if>
</@layout.registrationLayout>
