import { FormField, action, formAction } from "../Commons/forms"
import { createReducer } from "../Commons/forms"
import { InvitationInfo, RegistrationInfo, LicenseInfo, TokenResponseModel, StoreAuth, LoginWithPassword } from "../Api/api"
import { isChrome } from "../Commons/browser"

interface IAuthState {
    email:  FormField<string>;
    name: FormField<string>;
    company: FormField<string>;
    phoneNumber: FormField<string>;
    terms: FormField<boolean>;
    password: FormField<string>;
    passwordScore: number;
    passwordVisible: boolean;
    rememberMe: FormField<boolean>;

    invitation: InvitationInfo;
    registration: RegistrationInfo;
    license: LicenseInfo;

    inviteToken: string;
    emailToken: FormField<string>;
    passwordToken: FormField<string>;
    redirectUri : string;

    isSubmitting: boolean;
    isShaking: boolean;    
    isInitializing: boolean;
}

let initialState: IAuthState = {
    email: {},
    name: {},
    company: {},
    phoneNumber: {},
    terms: {},
    password: {},
    passwordScore: 0,
    passwordVisible: false,
    rememberMe: { value: true },

    invitation: { networkName: null },
    registration: { avatarUrl: null },
    license: { },

    inviteToken: null,
    emailToken: {},
    passwordToken: {},
    redirectUri: "/",

    isSubmitting: false,
    isShaking: false,
    isInitializing: true
};

const actionHandlers = {
    actionPrefix : "Auth"
};

export const
    setEmail = formAction<string>("email", { actionHandlers }),
    setName = formAction<string>("name", { actionHandlers }),
    setCompany = formAction<string>("company", { actionHandlers }),
    setPhoneNumber = formAction<string>("phoneNumber", { actionHandlers }),
    setPassword = formAction<string>("password", { actionHandlers }),
    setRememberMe = formAction<boolean>("rememberMe", { actionHandlers }),
    setTerms = formAction<boolean>("terms", { actionHandlers }),
    startSubmitting = () => action<boolean>("isSubmitting", { actionHandlers })(true),
    endSubmitting = () => action<boolean>("isSubmitting", { actionHandlers })(false),
    startShaking = () => action<boolean>("isShaking", { actionHandlers })(true),
    endShaking = () => action<boolean>("isShaking", { actionHandlers })(false),
    setInitialized = () => action<boolean>("isInitializing", { actionHandlers })(false),
    setPasswordScore = action<number>("passwordScore", { actionHandlers }),
    setPasswordVisible = action<boolean>("passwordVisible", { actionHandlers }),
    setInvitationInfo = action<InvitationInfo>("invitation", { actionHandlers }),
    setRegistrationInfo = action<RegistrationInfo>("registration", { actionHandlers }),
    setLicenseInfo = action<LicenseInfo>("license", { actionHandlers }),

    setInviteToken = action<string>("inviteToken", { actionHandlers }),
    setEmailToken = formAction<string>("emailToken", { actionHandlers }),
    setPasswordToken = formAction<string>("passwordToken", { actionHandlers }),
    setRedirectUri = action<string>("redirectUri", { actionHandlers });



export function scorePassword(password: string) {
    return dispatch => {        
        requirejs(["zxcvbn"], function (zxcvbn) {
            let score = zxcvbn(password).score;
            if (password.length < 6)
                score = Math.min(1, score)
            dispatch(setPasswordScore(score));
        });
    }
}

const LOGIN = "Auth/LOGIN";
export function loginWithToken(token: TokenResponseModel) {        
    StoreAuth(token);
    return {
        token: token,
        type: LOGIN
    }
}
actionHandlers[LOGIN] = (state: IAuthState, action) => {
    rememberAutofillData(state.email.value, state.password.value, state.redirectUri);
    return state;
}

export function rememberAutofillData(email: string, password: string, redirect: string) {    
    // send credential via form so the browser / password manager can intercept and store credentials    
    if (isChrome || !email || !password) {
        location.href = redirect;        
    }    

    if (!(/^http/.test(redirect))) {
        redirect = location.protocol + "//" + location.host + redirect;
    }
    $('#loginForm input[name="username"]').val(email);
    $('#loginForm input[name="password"]').val(password);
    $('#loginForm input[name="redirectUri"]').val(redirect);
    $('#loginForm #loginButton').click();
}

export function shakeForm() {
    return dispatch => {
        dispatch(startShaking());
        setTimeout(() => {
            dispatch(endShaking());
        }, 1000)
    }
}

export let reducer = createReducer(initialState, actionHandlers);
