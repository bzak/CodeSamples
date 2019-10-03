import * as React from "react"
import { connectedForm, FormField } from "../Commons/forms"
import { loginWithToken, startSubmitting, endSubmitting, shakeForm, setEmailToken } from "./Actions"
import { LaddaButton } from "../Tags/LaddaButton"
import { AccountApi, API_CLIENT_ID } from "../Api/api"
import { push } from 'react-router-redux'
import { error } from "../Commons/actions"
import { t, thtml } from "../i18n/translate"

interface ConfirmEmailProps {
    location: HistoryModule.Location;
    emailToken: FormField<string>;
    isShaking: boolean;
    isSubmitting: boolean;

    resendToken: (userId: string) => void;
    confirmEmail: (userId:string, token:string) => void;
}
const boundState = (state) => state.auth;
const boundProps = ['emailToken', 'isShaking', 'isSubmitting'];
const boundActions = { resendToken, confirmEmail };

export default connectedForm(boundState, boundProps, boundActions)(
    class ConfirmEmail extends React.Component<ConfirmEmailProps, {}> {
        getUserId() {
            return this.props.location.query["userId"];
        }
        getToken() {
            return this.props.location.query["token"];
        }
        componentDidMount() {        
            this.props.confirmEmail(this.getUserId(), this.getToken());
        }
        render() {
            return (
                <div>
                    { !this.props.emailToken.hasError &&
                        <div>
                            <h1 className="login-title">
                                {t("Auth:We are confirming your email address")}                                
                            </h1>
                            <LaddaButton loading={true}                    
                            className="btn btn-lg btn-primary" buttonStyle="contract"
                            type="submit">{t("Auth:Validating")}</LaddaButton>
                        </div>
                    }
                    { this.props.emailToken.hasError &&
                        <div className="has-error">
                            <h1 className="login-title">
                            {t("Auth:The link used to confirm email was expired or invalid")}.
                            </h1>
                            <LaddaButton loading={this.props.isSubmitting}
                                onClick={() => { this.props.resendToken(this.getUserId()) } }
                                className="btn btn-lg btn-primary btn-block"
                                type="submit">{t("Auth:Send me the link again")}</LaddaButton>
                        </div>
                    }
                </div>
            )
        }
    }
)

function confirmEmail(userId: string, token: string) {
    return dispatch => {
        dispatch(startSubmitting());

        let api = new AccountApi();
        api.accountConfirmEmail({
            command: {
                userId: userId,
                token: token,
                apiClientId: API_CLIENT_ID
            }
        }).then((token) => {
            dispatch(loginWithToken(token));
            dispatch(endSubmitting());
        }).catch((exception) => {
            dispatch(setEmailToken({ value: token, hasError: true }));
            dispatch(shakeForm());
            dispatch(endSubmitting());
        });
    }
}

function resendToken(userId: string) {
    return dispatch => {
        dispatch(startSubmitting());

        let api = new AccountApi();
        api.accountSendEmailConfirmationToken({
            command: {
                userId: userId
            }
        }).then((result) => {
            dispatch(push("/auth/confirm-email-sent"));
            dispatch(endSubmitting());
        }).catch((exception) => {
            dispatch(error("Error_Sending_Email"))
            dispatch(endSubmitting());
        });
    }
}


