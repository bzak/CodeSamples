import * as React from "react"
import { t, thtml } from "../i18n/translate"

export default class ConfirmEmailSent extends React.Component<{}, {}> {
    render() {
        return (
            <div>
                <h1 className="login-title">
                    {thtml("Auth:A message with an email confirmation link shall arrive shortly")}                    
                </h1>
            </div>
        );
    }
}