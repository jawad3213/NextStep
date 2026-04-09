<#import "template.ftl" as layout>
<@layout.registrationLayout displayMessage=!messagesPerField.existsError('firstName','lastName','email','username','password','password-confirm'); section>
    <#if section = "header">
        ${msg("registerTitle")}
    <#elseif section = "form">
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
                    <span>or sign up with email</span>
                </div>
            </div>
        </#if>

        <form id="kc-register-form" class="form" action="${url.registrationAction}" method="post">

            <div style="display: flex; gap: 1rem; margin-bottom: 1.5rem;">
                <div class="form-group" style="flex: 1; margin-bottom: 0;">
                    <label for="firstName">${msg("firstName")}</label>
                    <div class="input-wrapper">
                        <i class="fa fa-user input-icon"></i>
                        <input type="text" id="firstName" name="firstName" value="${(register.formData.firstName!'')}" autocomplete="given-name" />
                    </div>
                </div>

                <div class="form-group" style="flex: 1; margin-bottom: 0;">
                    <label for="lastName">${msg("lastName")}</label>
                    <div class="input-wrapper">
                        <i class="fa fa-user input-icon"></i>
                        <input type="text" id="lastName" name="lastName" value="${(register.formData.lastName!'')}" autocomplete="family-name" />
                    </div>
                </div>
            </div>

            <div class="form-group">
                <label for="email">${msg("email")}</label>
                <div class="input-wrapper">
                    <i class="fa fa-envelope input-icon"></i>
                    <input type="email" id="email" name="email" value="${(register.formData.email!'')}" autocomplete="email" />
                </div>
            </div>

            <#if !realm.registrationEmailAsUsername>
                <div class="form-group">
                    <label for="username">${msg("username")}</label>
                    <div class="input-wrapper">
                        <i class="fa fa-user-circle input-icon"></i>
                        <input type="text" id="username" name="username" value="${(register.formData.username!'')}" autocomplete="username" />
                    </div>
                </div>
            </#if>

            <#if passwordRequired ?? && passwordRequired>
                <div style="display: flex; gap: 1rem; margin-bottom: 1.5rem;">
                    <div class="form-group" style="flex: 1; margin-bottom: 0;">
                        <label for="password">${msg("password")}</label>
                        <div class="input-wrapper">
                            <i class="fa fa-lock input-icon"></i>
                            <input type="password" id="password" name="password" autocomplete="new-password" />
                        </div>
                    </div>

                    <div class="form-group" style="flex: 1; margin-bottom: 0;">
                        <label for="password-confirm">${msg("passwordConfirm")}</label>
                        <div class="input-wrapper">
                            <i class="fa fa-lock input-icon"></i>
                            <input type="password" id="password-confirm" name="password-confirm" />
                        </div>
                    </div>
                </div>
            </#if>

            <#if recaptchaRequired??>
                <div class="form-group">
                    <div class="g-recaptcha" data-size="compact" data-sitekey="${recaptchaSiteKey}"></div>
                </div>
            </#if>

            <div class="form-group" style="margin-top: 1rem;">
                <button class="btn-primary" type="submit">${msg("doRegister")}</button>
            </div>
            
        </form>
        
    <#elseif section = "info">
        <div id="kc-registration">
            <span>Already have an account? <a href="${url.loginUrl}">${msg("backToLogin")}</a></span>
        </div>
    </#if>
</@layout.registrationLayout>
