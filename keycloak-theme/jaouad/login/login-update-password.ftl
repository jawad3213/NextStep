<#import "template.ftl" as layout>
<@layout.registrationLayout displayMessage=true; section>
    <#if section = "header">
        ${msg("updatePasswordTitle")}
    <#elseif section = "form">
        <div class="auth-form-section">
            <h1 class="auth-title">Reset your password</h1>
            <p class="auth-subtitle">Choose a new password for your account.</p>

            <form id="kc-passwd-update-form" action="${url.loginAction}" method="post">
                <div class="auth-field">
                    <label for="password-new">${msg("passwordNew")}</label>
                    <div class="auth-input-wrapper">
                        <input type="password" id="password-new" name="password-new" autofocus autocomplete="new-password" placeholder="New password" />
                        <button type="button" class="auth-password-toggle" onclick="togglePwd('password-new','pn-icon')" tabindex="-1">
                            <i data-feather="eye" id="pn-icon"></i>
                        </button>
                    </div>
                    <#if messagesPerField.existsError('password')>
                        <span class="auth-error-text">
                            ${kcSanitize(messagesPerField.get('password'))?no_esc}
                        </span>
                    </#if>
                </div>

                <div class="auth-field">
                    <label for="password-confirm">${msg("passwordConfirm")}</label>
                    <div class="auth-input-wrapper">
                        <input type="password" id="password-confirm" name="password-confirm" autocomplete="new-password" placeholder="Confirm new password" />
                        <button type="button" class="auth-password-toggle" onclick="togglePwd('password-confirm','pc-icon')" tabindex="-1">
                            <i data-feather="eye" id="pc-icon"></i>
                        </button>
                    </div>
                    <#if messagesPerField.existsError('password-confirm')>
                        <span class="auth-error-text">
                            ${kcSanitize(messagesPerField.get('password-confirm'))?no_esc}
                        </span>
                    </#if>
                </div>

                <#if logoutSessions??>
                    <div class="auth-remember">
                        <label class="auth-checkbox-label">
                            <input type="checkbox" id="logout-sessions" name="logout-sessions" value="on" checked>
                            <span>${msg("logoutOtherSessions")}</span>
                        </label>
                    </div>
                </#if>

                <button class="auth-btn-primary" type="submit">Reset password</button>
            </form>
        </div>

        <script>
            function togglePwd(iId, icId) {
                var i = document.getElementById(iId);
                var ic = document.getElementById(icId);
                if (i.type === 'password') {
                    i.type = 'text';
                    ic.outerHTML = '<i data-feather="eye-off" id="' + icId + '"></i>';
                } else {
                    i.type = 'password';
                    ic.outerHTML = '<i data-feather="eye" id="' + icId + '"></i>';
                }
                if (typeof feather !== 'undefined') feather.replace({ width: 16, height: 16 });
            }
        </script>
    </#if>
</@layout.registrationLayout>
