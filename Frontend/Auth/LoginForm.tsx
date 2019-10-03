import * as React from "react"
import classnames = require("classnames");
import { connectedForm, bindFormField, UpdateField, FormField } from "../Commons/forms"
import { LaddaButton } from "../Tags/LaddaButton"
import { setPassword, setRememberMe, shakeForm, startSubmitting, endSubmitting, loginWithToken, setInitialized } from "./Actions"
import { Link } from 'react-router'
import { push } from 'react-router-redux'
import { LoginWithPassword } from "../Api/api"
import { error } from "../Commons/actions"
import { t } from "../i18n/translate"

interface LoginFormProps  {
    email: FormField<string>
    password: FormField<string>
    rememberMe: FormField<boolean>
    isShaking: boolean;
    isSubmitting: boolean;    
    isInitialized: boolean;    

    setPassword: UpdateField<string>
    setRememberMe: UpdateField<boolean>
    login: (formData: LoginFormProps) => void
    tryLogin: (email: string, password: string) => void
    showEmailForm: () => void
    setInitialized: () => void
}

const boundState = (state) => state.auth;
const boundProps = ['email', 'password', 'rememberMe', 'isShaking', 'isSubmitting', 'isInitialized'];
const boundActions = { login, setPassword, setRememberMe, showEmailForm, tryLogin, setInitialized }

export default connectedForm(boundState, boundProps, boundActions)(
    class LoginForm extends React.Component<LoginFormProps, {}> {    
        submit(e) {
            e.preventDefault();
            this.props.login(this.props);
        }
        toggleRememberMe() {
            this.props.setRememberMe({ value: !this.props.rememberMe.value });
        }
        componentWillMount() {
            if (!this.props.email.value)
                this.props.showEmailForm();
        }
        componentDidMount() {
            $("#passwordFrame").attr("src", "/api/account/login-form?username=" + encodeURIComponent(this.props.email.value));
            let that = this;
            $('#passwordFrame').load(function () {
                let passFrameValue = $('#passwordFrame').contents().find('input[name="password"]').val();
                if ((passFrameValue && passFrameValue.length > 0) && (!that.props.password.value || that.props.password.value.length == 0)) {                    
                    that.props.setPassword({
                        value: passFrameValue,
                        hasError: false
                    });
                    that.props.tryLogin(that.props.email.value, passFrameValue)
                }                                           
                $("input[name='password']");
            });

            $("#passwordInput").on('animationstart', (e) => {                
                that.props.tryLogin(that.props.email.value, $("#passwordInput").val())
            })            
        }
        componentWillReceiveProps(nextProps: LoginFormProps) {
            if (this.props.isInitialized) return;            
            
            if (!(this.props.password && this.props.password.value) && nextProps.password && nextProps.password.value && nextProps.password.value) {
                this.props.tryLogin(nextProps.email.value, nextProps.password.value);
            }            
        }        
        render() {
            return (
                <div>
                    <div className="back-btn">
                        <a style={{ cursor:"pointer" }} onClick={() => this.props.showEmailForm()}><i className="glyphicon glyphicon-arrow-left" /></a>
                    </div>
                    <img src="/content/profile.png" alt="profile" className="img-profile img-circle" width="100" height="100"/>
                    <h1 className="login-title">{this.props.email.value}</h1>
                    <form className="form-signin" onSubmit={(e) => this.submit(e)} name="formSignin">
                        <div className={
                            classnames({
                                "form-group text-left": true,
                                "has-error": this.props.password.hasError,
                                "shake": this.props.isShaking
                            }) }>
                            <label className="control-label">{t("Auth:Your password")}</label>
                            <input type="text" name="username" value={this.props.email.value} className="hidden" onChange={e => { }} />
                            <input type="password" name="password" className="form-control input-lg" placeholder={t("Auth:Password")} autoFocus id="passwordInput"
                                    onChange={ bindFormField(this.props.setPassword) }
                                    value={this.props.password.value} />
                        
                        </div>

                        <LaddaButton loading={this.props.isSubmitting}
                            className="btn btn-lg btn-primary btn-block"
                            type="submit">{t("Auth:Sign in")}</LaddaButton>

                        <div className="form-group text-left">
                            <label className="checkbox">
                                <input type="checkbox" name="terms" className="checkbox"
                                    onClick={ (e) => this.toggleRememberMe() }
                                    defaultChecked={this.props.rememberMe.value}/>
                                <Link to="/auth/recover-password" className="pull-right">{t("Auth:Forgot password?")}</Link>
                                <span>
                                    {t("Auth:Stay signed in")}
                                </span>
                            </label>
                        </div>
                    
                    </form>
                </div>           
            )
        }
    }
)

export function showEmailForm() {
    return dispatch => {
        dispatch(push("/auth"))
    }
}

declare var formSignin: any;

function login(formData: LoginFormProps) {
    return dispatch => {
        let valid = true;
        if (!formData.email.value) {
            valid = false;
        }
        if (!formData.password.value) {
            dispatch(setPassword({ value: formData.password.value, hasError: true }));
            valid = false;
        }
        if (!valid) {
            dispatch(shakeForm());
        }
        else {
            dispatch(startSubmitting());
            LoginWithPassword(formData.email.value, formData.password.value, formData.rememberMe.value)
                .then((token) => {
                    if ((window.external as any) && (window.external as any).AutoCompleteSaveForm) {
                        (window.external as any).AutoCompleteSaveForm(formSignin);
                    }
                    dispatch(loginWithToken(token));
                }).catch((exception) => {
                    console.log(exception); // todo log exception and show correct error message
                    dispatch(setPassword({ value: formData.password.value, hasError: true }));
                    dispatch(error("Invalid_Password"))
                    dispatch(shakeForm());
                    dispatch(endSubmitting());
                });
        }
    }
}

function tryLogin(email: string, password: string) {
    return dispatch => {
        if (!email || !password) return;
        dispatch(setInitialized());
        
        LoginWithPassword(email, password, true)
            .then((token) => {
                if ((window.external as any) && (window.external as any).AutoCompleteSaveForm) {
                    (window.external as any).AutoCompleteSaveForm(formSignin);
                }
                dispatch(loginWithToken(token));
            }).catch((exception) => {                                
            });
    }
}