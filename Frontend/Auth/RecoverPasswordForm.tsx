import * as React from "react"
import classnames = require("classnames");
import { connectedForm, validateEmail, bindFormField, UpdateField, FormField } from "../Commons/forms"
import { LaddaButton } from "../Tags/LaddaButton"
import { setEmail, shakeForm, startSubmitting, endSubmitting } from "./Actions"
import { push } from 'react-router-redux'
import { AccountApi } from "../Api/api"
import { error } from "../Commons/actions"
import { t, thtml } from "../i18n/translate"

interface RecoverPasswordFormProps {        
    email: FormField<string>
    isShaking: boolean;
    isSubmitting: boolean;

    setEmail: UpdateField<string>
    recoverPassword: (email: string) => void
}

const boundState = (state) => state.auth;
const boundProps = ['email', 'isShaking', 'isSubmitting'];
const boundActions = { setEmail, recoverPassword }

export default connectedForm(boundState, boundProps, boundActions)(
    class RecoverPasswordForm extends React.Component<RecoverPasswordFormProps, {}> {
        submit(e) {
            e.preventDefault();
            this.props.recoverPassword(this.props.email.value);
        }
        render() {
            return (
                <div>
                    <h1 className="login-title">{t("Auth:Please type in your email address to recover your password")}</h1>
                    <form className="form-signin" onSubmit={ (e) => this.submit(e) }>
                        <div className={
                            classnames({
                                "form-group text-left": true,
                                "has-error": this.props.email.hasError,
                                "shake": this.props.isShaking
                            }) }>
                            <label className="control-label">{t("Auth:Your email")}</label>
                            <input type="email" className="form-control input-lg" placeholder={t("Auth:Email address")} autoFocus
                                value={this.props.email.value} name="email"
                                onChange={ bindFormField(this.props.setEmail) } />
                            { this.props.email.hasError &&
                                <span className="help-block">{t("Auth:The email address is not valid")}</span>
                            }
                        </div>
                        <LaddaButton loading={this.props.isSubmitting}
                            className="btn btn-lg btn-primary btn-block"
                            type="submit">{t("Auth:Next")}</LaddaButton>
                    </form>
                </div>            
            )
        }
    }
)

export class RecoverPasswordFormSubmitted extends React.Component<{}, {}> {
    render() {
        return (
            <div>
                <h1 className="login-title">
                    {thtml("Auth:A message with a password reset link shall arrive shortly")}                    
                </h1>
            </div>
        );
    }
}


function recoverPassword(email) {
    return dispatch => {
        if (!validateEmail(email)) {
            dispatch(shakeForm());
            dispatch(setEmail({ value: email, hasError: true }));
        } else {
            dispatch(startSubmitting());

            let api = new AccountApi();
            api.accountSendPasswordResetToken({
                command: {
                    email: email
                }
            }).then((result) => {
                dispatch(push("/auth/recover-password-submitted"));
                dispatch(endSubmitting());
            }).catch((exception) => {
                //dispatch(error("Error_Sending_Email"))
                dispatch(shakeForm());
                dispatch(setEmail({ value: email, hasError: true }));
                dispatch(endSubmitting());
            });
        }
    }
}

