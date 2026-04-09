<#import "template.ftl" as layout>
<@layout.registrationLayout displayMessage=!messagesPerField.existsError('username','password') displayInfo=realm.password && realm.registrationAllowed && !registrationDisabled??; section>
    <#if section = "header">
        ${msg("loginAccountTitle")}
    <#elseif section = "form">
        <div id="kc-form">
            <div id="kc-form-wrapper">
                <#if realm.password>
                    <div id="kc-social-providers">
                        <div class="premium-nudge">
                            <span style="font-size: 14px;">⚡</span>
                            <p class="premium-nudge-text"><strong>Premium members</strong> get 3× more recruiter visibility — unlock after sign in.</p>
                        </div>
                        <div class="social-providers">
                            <a href="${url.loginUrl}&kc_idp_hint=google" class="btn-social" id="social-google">
                                <svg xmlns="http://www.w3.org/2000/svg" width="18" height="18" viewBox="0 0 48 48"><path fill="#FFC107" d="M43.611,20.083H42V20H24v8h11.303c-1.649,4.657-6.08,8-11.303,8c-6.627,0-12-5.373-12-12c0-6.627,5.373-12,12-12c3.059,0,5.842,1.154,7.961,3.039l5.657-5.657C34.046,6.053,29.268,4,24,4C12.955,4,4,12.955,4,24c0,11.045,8.955,20,20,20c11.045,0,20-8.955,20-20C44,22.659,43.862,21.35,43.611,20.083z"/><path fill="#FF3D00" d="M6.306,14.691l6.571,4.819C14.655,15.108,18.961,12,24,12c3.059,0,5.842,1.154,7.961,3.039l5.657-5.657C34.046,6.053,29.268,4,24,4C16.318,4,9.656,8.337,6.306,14.691z"/><path fill="#4CAF50" d="M24,44c5.166,0,9.86-1.977,13.409-5.192l-6.19-5.238C29.211,35.091,26.715,36,24,36c-5.202,0-9.619-3.317-11.283-7.946l-6.522,5.025C9.505,39.556,16.227,44,24,44z"/><path fill="#1976D2" d="M43.611,20.083H42V20H24v8h11.303c-0.792,2.237-2.231,4.166-4.087,5.571c0.001-0.001,0.002-0.001,0.003-0.002l6.19,5.238C36.971,39.205,44,34,44,24C44,22.659,43.862,21.35,43.611,20.083z"/></svg>
                                Google
                            </a>
                            <a href="${url.loginUrl}&kc_idp_hint=github" class="btn-social" id="social-github">
                                <svg xmlns="http://www.w3.org/2000/svg" width="18" height="18" viewBox="0 0 24 24"><path fill="#ffffff" d="M12 0c-6.626 0-12 5.373-12 12 0 5.302 3.438 9.8 8.207 11.387.599.111.793-.261.793-.577v-2.234c-3.338.726-4.033-1.416-4.033-1.416-.546-1.387-1.333-1.756-1.333-1.756-1.089-.745.083-.729.083-.729 1.205.084 1.839 1.237 1.839 1.237 1.07 1.834 2.807 1.304 3.492.997.107-.775.418-1.305.762-1.604-2.665-.305-5.467-1.334-5.467-5.931 0-1.311.469-2.381 1.236-3.221-.124-.303-.535-1.524.117-3.176 0 0 1.008-.322 3.301 1.23.957-.266 1.983-.399 3.003-.404 1.02.005 2.047.138 3.006.404 2.291-1.552 3.297-1.23 3.297-1.23.653 1.653.242 2.874.118 3.176.77.84 1.235 1.911 1.235 3.221 0 4.609-2.807 5.624-5.479 5.921.43.372.823 1.102.823 2.222v3.293c0 .319.192.694.801.576 4.765-1.589 8.199-6.086 8.199-11.386 0-6.627-5.373-12-12-12z"/></svg>
                                GitHub
                            </a>
                        </div>
                        <div class="social-divider">
                            <span>or sign in with email</span>
                        </div>

                        <#if messagesPerField.existsError('username','password')>
                            <div class="form-error-alert">
                                <i class="fa-solid fa-circle-exclamation"></i>
                                <span>${kcSanitize(messagesPerField.getFirstError('username','password'))?no_esc}</span>
                            </div>
                        </#if>
                    </div>
                </#if>

                <form id="kc-form-login" onsubmit="login.disabled = true; return true;" action="${url.loginAction}" method="post">
                    
                    <div class="form-group">
                        <label for="username"><#if !realm.loginWithEmailAllowed>${msg("username")}<#elseif !realm.registrationEmailAsUsername>${msg("usernameOrEmail")}<#else>${msg("email")}</#if></label>
                        <div class="input-wrapper <#if messagesPerField.existsError('username','password')>input-error</#if>">
                            <i class="fa fa-envelope input-icon"></i>
                            <input tabindex="1" id="username" class="" name="username" value="${(login.username!'')}" type="text" autofocus autocomplete="username" placeholder="name@company.com" />
                        </div>
                    </div>

                    <div class="form-group">
                        <label for="password">${msg("password")}</label>
                        <div class="input-wrapper <#if messagesPerField.existsError('username','password')>input-error</#if>">
                            <i class="fa fa-lock input-icon"></i>
                            <input tabindex="2" id="password" class="" name="password" type="password" autocomplete="current-password" placeholder="••••••••" />
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

                    <div class="form-group" style="margin-top: 1rem;">
                        <input type="hidden" id="id-hidden-input" name="credentialId" <#if auth.selectedCredential?has_content>value="${auth.selectedCredential}"</#if>/>
                        <button tabindex="4" class="btn-primary" name="login" id="kc-login" type="submit">Sign In & Find My Match &rarr;</button>
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
