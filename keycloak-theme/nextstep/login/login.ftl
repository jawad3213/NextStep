<#import "template.ftl" as layout>
<@layout.registrationLayout displayMessage=!messagesPerField.existsError('username','password') displayInfo=realm.password && realm.registrationAllowed && !registrationDisabled??; section>
    <#if section = "header">
        ${msg("loginAccountTitle")}
    <#elseif section = "form">
        <div id="kc-form">
            <div id="kc-form-wrapper">
                <#if realm.password>
                    <div id="kc-social-providers">
                        <div class="social-providers">
                            <#if social.providers?? && social.providers?size &gt; 0>
                                <#list social.providers as p>
                                    <a href="${p.loginUrl}" class="btn-social" id="social-${p.alias}">
                                        <i class="fa-brands fa-${p.alias}" style="font-size: 1.2rem; <#if p.alias == 'google'>color: #DB4437;<#elseif p.alias == 'github'>color: #333;</#if>"></i>
                                        ${p.displayName!}
                                    </a>
                                </#list>
                            <#else>
                                <a href="#" class="btn-social" id="social-google" onclick="return false;">
                                    <i class="fa-brands fa-google" style="font-size: 1.2rem; color: #DB4437;"></i>
                                    Google
                                </a>
                                <a href="#" class="btn-social" id="social-github" onclick="return false;">
                                    <i class="fa-brands fa-github" style="font-size: 1.2rem; color: #333;"></i>
                                    GitHub
                                </a>
                            </#if>
                        </div>
                        <div class="social-divider">
                            <span>or sign in with email</span>
                        </div>
                    </div>
                </#if>

                <form id="kc-form-login" onsubmit="login.disabled = true; return true;" action="${url.loginAction}" method="post">
                    
                    <div class="form-group">
                        <label for="username"><#if !realm.loginWithEmailAllowed>${msg("username")}<#elseif !realm.registrationEmailAsUsername>${msg("usernameOrEmail")}<#else>${msg("email")}</#if></label>
                        <div class="input-wrapper">
                            <i class="fa fa-envelope input-icon"></i>
                            <input tabindex="1" id="username" class="" name="username" value="${(login.username!'')}" type="text" autofocus autocomplete="username" />
                        </div>
                        <#if messagesPerField.existsError('username','password')>
                            <span class="error-text" aria-live="polite">
                                ${kcSanitize(messagesPerField.getFirstError('username','password'))?no_esc}
                            </span>
                        </#if>
                    </div>

                    <div class="form-group">
                        <label for="password">${msg("password")}</label>
                        <div class="input-wrapper">
                            <i class="fa fa-lock input-icon"></i>
                            <input tabindex="2" id="password" class="" name="password" type="password" autocomplete="current-password" />
                            <button type="button" class="password-toggle" onclick="togglePassword()">
                                <i class="fa fa-eye" id="password-icon"></i>
                            </button>
                        </div>
                    </div>

                    <div class="form-options">
                        <#if realm.rememberMe && !usernameHidden??>
                            <label class="remember-me">
                                <#if login.rememberMe??>
                                    <input tabindex="3" id="rememberMe" name="rememberMe" type="checkbox" checked> ${msg("rememberMe")}
                                <#else>
                                    <input tabindex="3" id="rememberMe" name="rememberMe" type="checkbox"> ${msg("rememberMe")}
                                </#if>
                            </label>
                        </#if>
                        
                        <#if realm.resetPasswordAllowed>
                            <a tabindex="5" href="${url.loginResetCredentialsUrl}" class="forgot-password">${msg("doForgotPassword")}</a>
                        </#if>
                    </div>

                    <div class="form-group" style="margin-top: 2rem;">
                        <input type="hidden" id="id-hidden-input" name="credentialId" <#if auth.selectedCredential?has_content>value="${auth.selectedCredential}"</#if>/>
                        <button tabindex="4" class="btn-primary" name="login" id="kc-login" type="submit">${msg("doLogIn")}</button>
                    </div>
                </form>
            </div>
            
            <script>
                function togglePassword() {
                    const passwordInput = document.getElementById('password');
                    const passwordIcon = document.getElementById('password-icon');
                    if (passwordInput.type === 'password') {
                        passwordInput.type = 'text';
                        passwordIcon.classList.remove('fa-eye');
                        passwordIcon.classList.add('fa-eye-slash');
                    } else {
                        passwordInput.type = 'password';
                        passwordIcon.classList.remove('fa-eye-slash');
                        passwordIcon.classList.add('fa-eye');
                    }
                }
            </script>
        </div>

    <#elseif section = "info" >
        <#if realm.password && realm.registrationAllowed && !registrationDisabled??>
            <div id="kc-registration">
                <span>${msg("noAccount")} <a tabindex="6" href="${url.registrationUrl}">${msg("doRegister")}</a></span>
            </div>
        </#if>
    </#if>
</@layout.registrationLayout>
